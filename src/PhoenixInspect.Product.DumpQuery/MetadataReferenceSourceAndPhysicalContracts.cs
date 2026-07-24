using System.Collections.Immutable;
using PhoenixInspect.Core.Abstractions;

namespace PhoenixInspect.Product.DumpQuery;

/// <summary>Extends one exact metadata source end with every table used by draft reference resolution.</summary>
/// <remarks>
/// The existing source-end digest remains authoritative for TypeDef, TypeRef, and TypeSpec counts. ModuleRef,
/// AssemblyRef, File, and ExportedType counts are projected from the same retained exhaustive module-search fact.
/// </remarks>
public sealed class MetadataReferenceSourceEndIdentity : IEquatable<MetadataReferenceSourceEndIdentity>
{
    /// <summary>Gets the maximum complete row count admitted for any one draft reference table.</summary>
    public const int MaximumReferenceTableRowCount = StaticFieldV2Limits.MaximumTypeDefinitionRowCount;

    private const string CanonicalDomain = "metadata-v2-reference-source-end";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataReferenceSourceEndIdentity(
        MetadataSourceEndIdentity definitionSourceEnds,
        int moduleReferenceRowCount,
        int assemblyReferenceRowCount,
        int fileRowCount,
        int exportedTypeRowCount)
    {
        DefinitionSourceEnds = definitionSourceEnds;
        ModuleReferenceRowCount = moduleReferenceRowCount;
        AssemblyReferenceRowCount = assemblyReferenceRowCount;
        FileRowCount = fileRowCount;
        ExportedTypeRowCount = exportedTypeRowCount;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteSha256(definitionSourceEnds.Sha256, nameof(definitionSourceEnds));
        writer.WriteInt32(definitionSourceEnds.TypeReferenceRowCount);
        writer.WriteInt32(definitionSourceEnds.TypeSpecificationRowCount);
        writer.WriteInt32(moduleReferenceRowCount);
        writer.WriteInt32(assemblyReferenceRowCount);
        writer.WriteInt32(fileRowCount);
        writer.WriteInt32(exportedTypeRowCount);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact landed source ends to which this draft reference extension is bound.</summary>
    public MetadataSourceEndIdentity DefinitionSourceEnds { get; }

    /// <summary>Gets the exact metadata module shared by every draft reference table.</summary>
    public StaticFieldMetadataModuleIdentity SourceModule => DefinitionSourceEnds.SourceModule;

    /// <summary>Gets the exact physical TypeRef table row count.</summary>
    public int TypeReferenceRowCount => DefinitionSourceEnds.TypeReferenceRowCount;

    /// <summary>Gets the exact physical TypeSpec table row count.</summary>
    public int TypeSpecificationRowCount => DefinitionSourceEnds.TypeSpecificationRowCount;

    /// <summary>Gets the exact physical ModuleRef table row count.</summary>
    public int ModuleReferenceRowCount { get; }

    /// <summary>Gets the exact physical AssemblyRef table row count.</summary>
    public int AssemblyReferenceRowCount { get; }

    /// <summary>Gets the exact physical File table row count.</summary>
    public int FileRowCount { get; }

    /// <summary>Gets the exact physical ExportedType table row count.</summary>
    public int ExportedTypeRowCount { get; }

    /// <summary>Gets a defensive copy of the fixed-reference canonical draft source-end bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft source-end bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one exact draft reference-source-end extension.</summary>
    /// <param name="definitionSourceEnds">
    /// The exact landed source ends retaining the same exhaustive module-search fact as every projected count.
    /// </param>
    /// <returns>A fixed-reference draft identity covering all six reference-resolution tables.</returns>
    public static MetadataReferenceSourceEndIdentity Create(MetadataSourceEndIdentity definitionSourceEnds)
    {
        ArgumentNullException.ThrowIfNull(definitionSourceEnds);
        var fact = definitionSourceEnds.SourceModuleFact;
        if (fact.Status != StaticFieldModuleSearchStatus.Exact ||
            fact.ModuleReferenceRowCount is not { } moduleReferenceRowCount ||
            fact.AssemblyReferenceRowCount is not { } assemblyReferenceRowCount ||
            fact.FileRowCount is not { } fileRowCount ||
            fact.ExportedTypeRowCount is not { } exportedTypeRowCount)
        {
            throw new ArgumentException(
                "Reference source ends require complete row counts from the retained exact module-search fact.",
                nameof(definitionSourceEnds));
        }

        return new MetadataReferenceSourceEndIdentity(
            definitionSourceEnds,
            moduleReferenceRowCount,
            assemblyReferenceRowCount,
            fileRowCount,
            exportedTypeRowCount);
    }

    /// <summary>Tests whether one token lies inside the exact draft source end for its reference table.</summary>
    /// <param name="metadataToken">The non-nil metadata token to test.</param>
    /// <returns><see langword="true"/> only for an in-range token from one of the six covered tables.</returns>
    public bool ContainsReferenceToken(int metadataToken)
    {
        var rowId = CanonicalReplayEncoding.MetadataTokenRowId(metadataToken);
        return rowId > 0 && (metadataToken & unchecked((int)0xFF00_0000)) switch
        {
            0x0100_0000 => rowId <= TypeReferenceRowCount,
            0x1A00_0000 => rowId <= ModuleReferenceRowCount,
            0x1B00_0000 => rowId <= TypeSpecificationRowCount,
            0x2300_0000 => rowId <= AssemblyReferenceRowCount,
            0x2600_0000 => rowId <= FileRowCount,
            0x2700_0000 => rowId <= ExportedTypeRowCount,
            _ => false,
        };
    }

    /// <summary>Tests canonical equality between two exact draft reference-source-end identities.</summary>
    /// <param name="other">The other draft source-end identity.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(MetadataReferenceSourceEndIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests draft reference-source-end equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for an identity with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataReferenceSourceEndIdentity);

    /// <summary>Computes a deterministic hash code from fixed-reference canonical draft content.</summary>
    /// <returns>A hash code for this draft reference-source-end identity.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Classifies one complete physical draft reference-table catalog.</summary>
/// <remarks>Every non-exact or invalid result exposes no physical row prefix.</remarks>
public enum MetadataReferencePhysicalTableResultKind
{
    /// <summary>Every physical RID is present once in exact order.</summary>
    Exact = 1,

    /// <summary>Acquisition stopped, a prerequisite was non-exact, or a row bound was reached.</summary>
    NonExact = 2,

    /// <summary>Complete supplied rows contradicted the exact source end or table invariants.</summary>
    Invalid = 3,
}

/// <summary>Identifies one complete physical draft reference-table catalog issue.</summary>
/// <remarks>The shared issue set keeps prefix-free table-shape dispositions consistent across all six catalogs.</remarks>
public enum MetadataReferencePhysicalTableIssue
{
    /// <summary>No issue applies to an exact complete draft table.</summary>
    None = 0,

    /// <summary>The exact physical row count reached the declared table cap plus one.</summary>
    TableRowBoundReached = 1,

    /// <summary>The supplied observation vector was default or shorter than the exact source end.</summary>
    TableIncomplete = 2,

    /// <summary>The supplied observation vector exceeded the exact source end.</summary>
    TableRowCountConflict = 3,

    /// <summary>The supplied tokens did not form the exact physical RID sequence.</summary>
    PhysicalOrderInvalid = 4,

    /// <summary>A supplied row belonged to different exact reference source ends.</summary>
    SourceMismatch = 5,

    /// <summary>A physical coded index lay outside its exact target table end.</summary>
    CodedIndexOutOfRange = 6,

    /// <summary>A required complete physical table prerequisite stopped non-exactly.</summary>
    PrerequisiteNonExact = 7,

    /// <summary>A required complete physical table prerequisite retained contradictory evidence.</summary>
    PrerequisiteInvalid = 8,

    /// <summary>A required complete physical table prerequisite named different source ends.</summary>
    PrerequisiteSourceMismatch = 9,
}

internal static class MetadataReferencePhysicalTableValidation
{
    internal static MetadataReferencePhysicalTableIssue ValidateVector<T>(
        int expectedCount,
        ImmutableArray<T> observations,
        int tableId,
        Func<T, int> token,
        out MetadataReferencePhysicalTableResultKind resultKind,
        out EvaluationDeterministicBound? reachedBound,
        out int observedCount,
        string boundName)
        where T : class
    {
        reachedBound = null;
        observedCount = observations.IsDefault ? 0 : observations.Length;
        if (expectedCount > MetadataReferenceSourceEndIdentity.MaximumReferenceTableRowCount)
        {
            resultKind = MetadataReferencePhysicalTableResultKind.NonExact;
            reachedBound = new EvaluationDeterministicBound(
                boundName,
                MetadataReferenceSourceEndIdentity.MaximumReferenceTableRowCount);
            observedCount = MetadataReferenceSourceEndIdentity.MaximumReferenceTableRowCount + 1;
            return MetadataReferencePhysicalTableIssue.TableRowBoundReached;
        }
        if (observations.IsDefault || observations.Length < expectedCount)
        {
            resultKind = MetadataReferencePhysicalTableResultKind.NonExact;
            return MetadataReferencePhysicalTableIssue.TableIncomplete;
        }
        if (observations.Length > expectedCount)
        {
            resultKind = MetadataReferencePhysicalTableResultKind.Invalid;
            return MetadataReferencePhysicalTableIssue.TableRowCountConflict;
        }
        for (var index = 0; index < observations.Length; index++)
        {
            if (observations[index] is null || token(observations[index]) != (tableId << 24 | index + 1))
            {
                resultKind = MetadataReferencePhysicalTableResultKind.Invalid;
                return MetadataReferencePhysicalTableIssue.PhysicalOrderInvalid;
            }
        }
        resultKind = MetadataReferencePhysicalTableResultKind.Exact;
        observedCount = expectedCount;
        return MetadataReferencePhysicalTableIssue.None;
    }
}

/// <summary>Freezes one source-anchored physical TypeRef-row draft observation.</summary>
/// <remarks>
/// This W8 observation retains the raw row payload without accepting a pre-resolved W7 target. Complete-table
/// authority is added only by <see cref="MetadataTypeReferencePhysicalTableCatalogIdentity"/>.
/// </remarks>
public sealed class MetadataTypeReferenceRowObservationIdentity :
    IEquatable<MetadataTypeReferenceRowObservationIdentity>
{
    private const string CanonicalDomain = "metadata-v2-typeref-row-observation";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataTypeReferenceRowObservationIdentity(
        StaticFieldMetadataModuleIdentity sourceModule,
        int typeReferenceToken,
        string namespaceName,
        string typeName,
        int resolutionScopeMetadataToken)
    {
        SourceModule = sourceModule;
        TypeReferenceToken = typeReferenceToken;
        NamespaceName = namespaceName;
        TypeName = typeName;
        ResolutionScopeMetadataToken = resolutionScopeMetadataToken;
        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteSha256(sourceModule.Sha256, nameof(sourceModule));
        writer.WriteInt32(typeReferenceToken);
        writer.WriteString(namespaceName);
        writer.WriteString(typeName);
        writer.WriteInt32(resolutionScopeMetadataToken);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact metadata module containing the physical TypeRef row.</summary>
    public StaticFieldMetadataModuleIdentity SourceModule { get; }

    /// <summary>Gets the exact non-nil physical TypeRef token.</summary>
    public int TypeReferenceToken { get; }

    /// <summary>Gets the exact decoded namespace, including empty for a nested row.</summary>
    public string NamespaceName { get; }

    /// <summary>Gets the exact decoded non-empty metadata type name.</summary>
    public string TypeName { get; }

    /// <summary>Gets the exact Module, TypeRef, ModuleRef, or AssemblyRef ResolutionScope token.</summary>
    public int ResolutionScopeMetadataToken { get; }

    /// <summary>Gets a defensive copy of the canonical physical draft observation bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft observation bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one source-anchored physical TypeRef-row draft observation.</summary>
    /// <param name="sourceModule">The exact metadata module containing the physical row.</param>
    /// <param name="typeReferenceToken">The exact non-nil TypeRef token.</param>
    /// <param name="namespaceName">The exact namespace, including empty for a nested TypeRef.</param>
    /// <param name="typeName">The exact non-empty metadata type name.</param>
    /// <param name="resolutionScopeMetadataToken">The exact non-nil decoded ResolutionScope token.</param>
    /// <returns>A physical W8 draft observation with no target-resolution assertion.</returns>
    public static MetadataTypeReferenceRowObservationIdentity Create(
        StaticFieldMetadataModuleIdentity sourceModule,
        int typeReferenceToken,
        string namespaceName,
        string typeName,
        int resolutionScopeMetadataToken)
    {
        ArgumentNullException.ThrowIfNull(sourceModule);
        CanonicalReplayEncoding.ValidateMetadataToken(typeReferenceToken, 0x01, nameof(typeReferenceToken));
        StaticFieldMetadataTextLimits.Validate(namespaceName, nameof(namespaceName), allowEmpty: true);
        if (namespaceName.Length > 0)
        {
            StaticFieldContractEncoding.RequireIdentifierValue(namespaceName, nameof(namespaceName), allowDots: true);
        }
        StaticFieldMetadataTextLimits.Validate(typeName, nameof(typeName), allowEmpty: false);
        StaticFieldContractEncoding.RequireIdentifierValue(typeName, nameof(typeName), allowDots: false);
        if (!CanonicalReplayEncoding.IsMetadataTokenForTable(resolutionScopeMetadataToken, 0x00) &&
            !CanonicalReplayEncoding.IsMetadataTokenForTable(resolutionScopeMetadataToken, 0x01) &&
            !CanonicalReplayEncoding.IsMetadataTokenForTable(resolutionScopeMetadataToken, 0x1A) &&
            !CanonicalReplayEncoding.IsMetadataTokenForTable(resolutionScopeMetadataToken, 0x23))
        {
            throw new ArgumentOutOfRangeException(nameof(resolutionScopeMetadataToken));
        }
        return new MetadataTypeReferenceRowObservationIdentity(
            sourceModule,
            typeReferenceToken,
            namespaceName,
            typeName,
            resolutionScopeMetadataToken);
    }

    /// <summary>Tests canonical equality between two physical TypeRef draft observations.</summary>
    /// <param name="other">The other draft observation.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(MetadataTypeReferenceRowObservationIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests physical TypeRef draft observation equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for an observation with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataTypeReferenceRowObservationIdentity);

    /// <summary>Computes a deterministic hash code from canonical physical draft content.</summary>
    /// <returns>A hash code for this physical TypeRef draft observation.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Freezes one guarded physical TypeRef draft row tied to complete reference source ends.</summary>
/// <remarks>The W8 row is retained as a physical observation only; it makes no target-resolution assertion.</remarks>
public sealed class MetadataTypeReferencePhysicalRowIdentity :
    IEquatable<MetadataTypeReferencePhysicalRowIdentity>
{
    private const string CanonicalDomain = "metadata-v2-typeref-physical-row";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataTypeReferencePhysicalRowIdentity(
        MetadataReferenceSourceEndIdentity sourceEnds,
        MetadataTypeReferenceRowObservationIdentity observation)
    {
        SourceEnds = sourceEnds;
        Observation = observation;
        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteSha256(sourceEnds.Sha256, nameof(sourceEnds));
        writer.WriteSha256(observation.Sha256, nameof(observation));
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact draft reference source ends containing this row.</summary>
    public MetadataReferenceSourceEndIdentity SourceEnds { get; }

    /// <summary>Gets the unchanged exact physical W8 TypeRef-row observation.</summary>
    public MetadataTypeReferenceRowObservationIdentity Observation { get; }

    /// <summary>Gets the exact non-nil physical TypeRef token.</summary>
    public int TypeReferenceToken => Observation.TypeReferenceToken;

    /// <summary>Gets a defensive copy of the fixed-reference canonical draft row bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of this canonical draft row.</summary>
    public string Sha256 { get; }

    /// <summary>Tests canonical equality between two guarded physical TypeRef draft rows.</summary>
    /// <param name="other">The other draft row.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(MetadataTypeReferencePhysicalRowIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests guarded physical TypeRef draft equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a row with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataTypeReferencePhysicalRowIdentity);

    /// <summary>Computes a deterministic hash code from fixed-reference canonical draft content.</summary>
    /// <returns>A hash code for this guarded physical TypeRef draft row.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static MetadataTypeReferencePhysicalRowIdentity Create(
        object mintCapability,
        MetadataReferenceSourceEndIdentity sourceEnds,
        MetadataTypeReferenceRowObservationIdentity observation)
    {
        if (!MetadataTypeReferencePhysicalTableCatalogIdentity.OwnsRowMintCapability(mintCapability))
        {
            throw new ArgumentException("A physical TypeRef row requires its complete table's private mint capability.");
        }
        return new MetadataTypeReferencePhysicalRowIdentity(sourceEnds, observation);
    }
}

/// <summary>Freezes one complete RID-ordered physical TypeRef draft table.</summary>
/// <remarks>ResolutionScope tokens are checked against exact Module, TypeRef, ModuleRef, and AssemblyRef ends.</remarks>
public sealed class MetadataTypeReferencePhysicalTableCatalogIdentity :
    IEquatable<MetadataTypeReferencePhysicalTableCatalogIdentity>
{
    /// <summary>Gets the deterministic draft bound name for physical TypeRef rows.</summary>
    public const string TableRowCountBoundName = "metadata-v2.physical-typeref.rows";

    private const string CanonicalDomain = "metadata-v2-typeref-physical-table";
    private const int CanonicalSchemaVersion = 1;
    private static readonly object RowMintCapability = new();
    private readonly ImmutableArray<MetadataTypeReferencePhysicalRowIdentity> rows;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataTypeReferencePhysicalTableCatalogIdentity(
        MetadataReferencePhysicalTableResultKind resultKind,
        MetadataReferencePhysicalTableIssue issue,
        MetadataReferenceSourceEndIdentity sourceEnds,
        ImmutableArray<MetadataTypeReferencePhysicalRowIdentity> rows,
        EvaluationDeterministicBound? reachedBound,
        int observedCount)
    {
        ResultKind = resultKind;
        Issue = issue;
        SourceEnds = sourceEnds;
        this.rows = ExpressionV2ContractEncoding.Copy(rows);
        ReachedBound = reachedBound;
        ObservedCount = observedCount;
        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32((int)resultKind);
        writer.WriteInt32((int)issue);
        writer.WriteSha256(sourceEnds.Sha256, nameof(sourceEnds));
        writer.WriteInt32(rows.Length);
        foreach (var row in rows)
        {
            writer.WriteSha256(row.Sha256, nameof(rows));
        }
        ExpressionV2ContractEncoding.WriteOptionalBound(writer, reachedBound);
        writer.WriteInt32(observedCount);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets whether this complete physical TypeRef draft table is exact, non-exact, or invalid.</summary>
    public MetadataReferencePhysicalTableResultKind ResultKind { get; }

    /// <summary>Gets the typed complete physical TypeRef draft issue.</summary>
    public MetadataReferencePhysicalTableIssue Issue { get; }

    /// <summary>Gets the exact draft reference source ends for this table.</summary>
    public MetadataReferenceSourceEndIdentity SourceEnds { get; }

    /// <summary>Gets a defensive RID-order copy of exact guarded draft rows, or an empty vector for every stop.</summary>
    public ImmutableArray<MetadataTypeReferencePhysicalRowIdentity> Rows =>
        ExpressionV2ContractEncoding.Copy(rows);

    /// <summary>Gets the physical TypeRef row-count draft bound after a cap-plus-one outcome.</summary>
    public EvaluationDeterministicBound? ReachedBound { get; }

    /// <summary>Gets the supplied, exact, or cap-plus-one draft observation count.</summary>
    public int ObservedCount { get; }

    /// <summary>Gets a defensive copy of the fixed-reference canonical draft table bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft table bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one complete RID-ordered physical TypeRef draft table.</summary>
    /// <param name="sourceEnds">The exact reference source ends defining table length and coded-index domains.</param>
    /// <param name="observations">Every source-anchored physical W8 TypeRef row in RID order.</param>
    /// <returns>An exact guarded table or a prefix-free typed draft stop.</returns>
    public static MetadataTypeReferencePhysicalTableCatalogIdentity Create(
        MetadataReferenceSourceEndIdentity sourceEnds,
        ImmutableArray<MetadataTypeReferenceRowObservationIdentity> observations)
    {
        ArgumentNullException.ThrowIfNull(sourceEnds);
        var issue = MetadataReferencePhysicalTableValidation.ValidateVector(
            sourceEnds.TypeReferenceRowCount,
            observations,
            0x01,
            static row => row.TypeReferenceToken,
            out var resultKind,
            out var reachedBound,
            out var observedCount,
            TableRowCountBoundName);
        if (issue != MetadataReferencePhysicalTableIssue.None)
        {
            return Stopped(resultKind, issue, sourceEnds, reachedBound, observedCount);
        }
        foreach (var row in observations)
        {
            if (!row.SourceModule.Equals(sourceEnds.SourceModule))
            {
                return Stopped(
                    MetadataReferencePhysicalTableResultKind.Invalid,
                    MetadataReferencePhysicalTableIssue.SourceMismatch,
                    sourceEnds,
                    null,
                    observations.Length);
            }
            var scope = row.ResolutionScopeMetadataToken;
            var inRange = CanonicalReplayEncoding.IsMetadataTokenForTable(scope, 0x00)
                ? scope == 0x0000_0001
                : sourceEnds.ContainsReferenceToken(scope);
            if (!inRange)
            {
                return Stopped(
                    MetadataReferencePhysicalTableResultKind.Invalid,
                    MetadataReferencePhysicalTableIssue.CodedIndexOutOfRange,
                    sourceEnds,
                    null,
                    observations.Length);
            }
        }
        var builder = ImmutableArray.CreateBuilder<MetadataTypeReferencePhysicalRowIdentity>(observations.Length);
        foreach (var observation in observations)
        {
            builder.Add(MetadataTypeReferencePhysicalRowIdentity.Create(RowMintCapability, sourceEnds, observation));
        }
        return new MetadataTypeReferencePhysicalTableCatalogIdentity(
            MetadataReferencePhysicalTableResultKind.Exact,
            MetadataReferencePhysicalTableIssue.None,
            sourceEnds,
            builder.MoveToImmutable(),
            null,
            observations.Length);
    }

    /// <summary>Finds one exact guarded physical TypeRef draft row by token.</summary>
    /// <param name="typeReferenceToken">The non-nil physical TypeRef token.</param>
    /// <returns>The exact draft row, or null when the table or token is not exact.</returns>
    public MetadataTypeReferencePhysicalRowIdentity? FindRow(int typeReferenceToken)
    {
        if (ResultKind != MetadataReferencePhysicalTableResultKind.Exact ||
            !CanonicalReplayEncoding.IsMetadataTokenForTable(typeReferenceToken, 0x01))
        {
            return null;
        }
        var rowId = CanonicalReplayEncoding.MetadataTokenRowId(typeReferenceToken);
        return rowId > 0 && rowId <= rows.Length ? rows[rowId - 1] : null;
    }

    /// <summary>Tests canonical equality between two complete physical TypeRef draft tables.</summary>
    /// <param name="other">The other draft table.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(MetadataTypeReferencePhysicalTableCatalogIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests complete physical TypeRef draft-table equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a table with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataTypeReferencePhysicalTableCatalogIdentity);

    /// <summary>Computes a deterministic hash code from fixed-reference canonical draft content.</summary>
    /// <returns>A hash code for this complete physical TypeRef draft table.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static bool OwnsRowMintCapability(object? capability) => ReferenceEquals(capability, RowMintCapability);

    private static MetadataTypeReferencePhysicalTableCatalogIdentity Stopped(
        MetadataReferencePhysicalTableResultKind resultKind,
        MetadataReferencePhysicalTableIssue issue,
        MetadataReferenceSourceEndIdentity sourceEnds,
        EvaluationDeterministicBound? reachedBound,
        int observedCount) =>
        new(resultKind, issue, sourceEnds, [], reachedBound, observedCount);
}

/// <summary>Freezes one physical ModuleRef-row draft observation.</summary>
/// <remarks>The observation retains only the exact token and decoded name; table authority is added by its catalog.</remarks>
public sealed class MetadataModuleReferenceRowObservationIdentity :
    IEquatable<MetadataModuleReferenceRowObservationIdentity>
{
    private const string CanonicalDomain = "metadata-v2-moduleref-row-observation";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataModuleReferenceRowObservationIdentity(
        StaticFieldMetadataModuleIdentity sourceModule,
        int moduleReferenceToken,
        string name)
    {
        SourceModule = sourceModule;
        ModuleReferenceToken = moduleReferenceToken;
        Name = name;
        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteSha256(sourceModule.Sha256, nameof(sourceModule));
        writer.WriteInt32(moduleReferenceToken);
        writer.WriteString(name);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact metadata module containing the physical ModuleRef row.</summary>
    public StaticFieldMetadataModuleIdentity SourceModule { get; }

    /// <summary>Gets the exact non-nil physical ModuleRef token.</summary>
    public int ModuleReferenceToken { get; }

    /// <summary>Gets the exact decoded non-empty ModuleRef name.</summary>
    public string Name { get; }

    /// <summary>Gets a defensive copy of the canonical physical draft observation bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft observation bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one physical ModuleRef-row draft observation.</summary>
    /// <param name="sourceModule">The exact metadata module containing the physical row.</param>
    /// <param name="moduleReferenceToken">The exact non-nil ModuleRef token.</param>
    /// <param name="name">The exact decoded non-empty ModuleRef name.</param>
    /// <returns>A physical draft observation that makes no module-target assertion.</returns>
    public static MetadataModuleReferenceRowObservationIdentity Create(
        StaticFieldMetadataModuleIdentity sourceModule,
        int moduleReferenceToken,
        string name)
    {
        ArgumentNullException.ThrowIfNull(sourceModule);
        CanonicalReplayEncoding.ValidateMetadataToken(moduleReferenceToken, 0x1A, nameof(moduleReferenceToken));
        StaticFieldMetadataTextLimits.Validate(name, nameof(name), allowEmpty: false);
        return new MetadataModuleReferenceRowObservationIdentity(sourceModule, moduleReferenceToken, name);
    }

    /// <summary>Tests canonical equality between two physical ModuleRef draft observations.</summary>
    /// <param name="other">The other draft observation.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(MetadataModuleReferenceRowObservationIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests physical ModuleRef draft observation equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for an observation with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataModuleReferenceRowObservationIdentity);

    /// <summary>Computes a deterministic hash code from canonical physical draft content.</summary>
    /// <returns>A hash code for this physical ModuleRef draft observation.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Freezes one guarded physical ModuleRef draft row tied to complete reference source ends.</summary>
/// <remarks>Only an exact complete ModuleRef table can mint this fixed-reference draft row.</remarks>
public sealed class MetadataModuleReferencePhysicalRowIdentity :
    IEquatable<MetadataModuleReferencePhysicalRowIdentity>
{
    private const string CanonicalDomain = "metadata-v2-moduleref-physical-row";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataModuleReferencePhysicalRowIdentity(
        MetadataReferenceSourceEndIdentity sourceEnds,
        MetadataModuleReferenceRowObservationIdentity observation)
    {
        SourceEnds = sourceEnds;
        Observation = observation;
        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteSha256(sourceEnds.Sha256, nameof(sourceEnds));
        writer.WriteSha256(observation.Sha256, nameof(observation));
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact draft reference source ends containing this row.</summary>
    public MetadataReferenceSourceEndIdentity SourceEnds { get; }

    /// <summary>Gets the unchanged physical ModuleRef-row draft observation.</summary>
    public MetadataModuleReferenceRowObservationIdentity Observation { get; }

    /// <summary>Gets the exact non-nil physical ModuleRef token.</summary>
    public int ModuleReferenceToken => Observation.ModuleReferenceToken;

    /// <summary>Gets a defensive copy of the fixed-reference canonical draft row bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft row bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Tests canonical equality between two guarded physical ModuleRef draft rows.</summary>
    /// <param name="other">The other draft row.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(MetadataModuleReferencePhysicalRowIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests guarded physical ModuleRef draft equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a row with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataModuleReferencePhysicalRowIdentity);

    /// <summary>Computes a deterministic hash code from fixed-reference canonical draft content.</summary>
    /// <returns>A hash code for this guarded physical ModuleRef draft row.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static MetadataModuleReferencePhysicalRowIdentity Create(
        object mintCapability,
        MetadataReferenceSourceEndIdentity sourceEnds,
        MetadataModuleReferenceRowObservationIdentity observation)
    {
        if (!MetadataModuleReferencePhysicalTableCatalogIdentity.OwnsRowMintCapability(mintCapability))
        {
            throw new ArgumentException("A physical ModuleRef row requires its complete table's private mint capability.");
        }
        return new MetadataModuleReferencePhysicalRowIdentity(sourceEnds, observation);
    }
}

/// <summary>Freezes one complete RID-ordered physical ModuleRef draft table.</summary>
/// <remarks>Every catalog-level stop is prefix-free and retains no guarded physical row.</remarks>
public sealed class MetadataModuleReferencePhysicalTableCatalogIdentity :
    IEquatable<MetadataModuleReferencePhysicalTableCatalogIdentity>
{
    /// <summary>Gets the deterministic draft bound name for physical ModuleRef rows.</summary>
    public const string TableRowCountBoundName = "metadata-v2.physical-moduleref.rows";

    private const string CanonicalDomain = "metadata-v2-moduleref-physical-table";
    private const int CanonicalSchemaVersion = 1;
    private static readonly object RowMintCapability = new();
    private readonly ImmutableArray<MetadataModuleReferencePhysicalRowIdentity> rows;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataModuleReferencePhysicalTableCatalogIdentity(
        MetadataReferencePhysicalTableResultKind resultKind,
        MetadataReferencePhysicalTableIssue issue,
        MetadataReferenceSourceEndIdentity sourceEnds,
        ImmutableArray<MetadataModuleReferencePhysicalRowIdentity> rows,
        EvaluationDeterministicBound? reachedBound,
        int observedCount)
    {
        ResultKind = resultKind;
        Issue = issue;
        SourceEnds = sourceEnds;
        this.rows = ExpressionV2ContractEncoding.Copy(rows);
        ReachedBound = reachedBound;
        ObservedCount = observedCount;
        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32((int)resultKind);
        writer.WriteInt32((int)issue);
        writer.WriteSha256(sourceEnds.Sha256, nameof(sourceEnds));
        writer.WriteInt32(rows.Length);
        foreach (var row in rows)
        {
            writer.WriteSha256(row.Sha256, nameof(rows));
        }
        ExpressionV2ContractEncoding.WriteOptionalBound(writer, reachedBound);
        writer.WriteInt32(observedCount);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets whether this physical ModuleRef draft table is exact, non-exact, or invalid.</summary>
    public MetadataReferencePhysicalTableResultKind ResultKind { get; }

    /// <summary>Gets the typed complete physical ModuleRef draft issue.</summary>
    public MetadataReferencePhysicalTableIssue Issue { get; }

    /// <summary>Gets the exact draft reference source ends for this table.</summary>
    public MetadataReferenceSourceEndIdentity SourceEnds { get; }

    /// <summary>Gets a defensive RID-order copy of exact guarded rows, or an empty vector for every stop.</summary>
    public ImmutableArray<MetadataModuleReferencePhysicalRowIdentity> Rows =>
        ExpressionV2ContractEncoding.Copy(rows);

    /// <summary>Gets the physical ModuleRef row-count draft bound after a cap-plus-one outcome.</summary>
    public EvaluationDeterministicBound? ReachedBound { get; }

    /// <summary>Gets the supplied, exact, or cap-plus-one draft observation count.</summary>
    public int ObservedCount { get; }

    /// <summary>Gets a defensive copy of the fixed-reference canonical draft table bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft table bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one complete RID-ordered physical ModuleRef draft table.</summary>
    /// <param name="sourceEnds">The exact reference source ends defining the physical table length.</param>
    /// <param name="observations">Every physical ModuleRef row in RID order.</param>
    /// <returns>An exact guarded table or a prefix-free typed draft stop.</returns>
    public static MetadataModuleReferencePhysicalTableCatalogIdentity Create(
        MetadataReferenceSourceEndIdentity sourceEnds,
        ImmutableArray<MetadataModuleReferenceRowObservationIdentity> observations)
    {
        ArgumentNullException.ThrowIfNull(sourceEnds);
        var issue = MetadataReferencePhysicalTableValidation.ValidateVector(
            sourceEnds.ModuleReferenceRowCount,
            observations,
            0x1A,
            static row => row.ModuleReferenceToken,
            out var resultKind,
            out var reachedBound,
            out var observedCount,
            TableRowCountBoundName);
        if (issue != MetadataReferencePhysicalTableIssue.None)
        {
            return new MetadataModuleReferencePhysicalTableCatalogIdentity(
                resultKind, issue, sourceEnds, [], reachedBound, observedCount);
        }
        if (observations.Any(row => !row.SourceModule.Equals(sourceEnds.SourceModule)))
        {
            return new MetadataModuleReferencePhysicalTableCatalogIdentity(
                MetadataReferencePhysicalTableResultKind.Invalid,
                MetadataReferencePhysicalTableIssue.SourceMismatch,
                sourceEnds,
                [],
                null,
                observations.Length);
        }
        var builder = ImmutableArray.CreateBuilder<MetadataModuleReferencePhysicalRowIdentity>(observations.Length);
        foreach (var observation in observations)
        {
            builder.Add(MetadataModuleReferencePhysicalRowIdentity.Create(RowMintCapability, sourceEnds, observation));
        }
        return new MetadataModuleReferencePhysicalTableCatalogIdentity(
            MetadataReferencePhysicalTableResultKind.Exact,
            MetadataReferencePhysicalTableIssue.None,
            sourceEnds,
            builder.MoveToImmutable(),
            null,
            observations.Length);
    }

    /// <summary>Finds one exact guarded physical ModuleRef draft row by token.</summary>
    /// <param name="moduleReferenceToken">The non-nil physical ModuleRef token.</param>
    /// <returns>The exact draft row, or null when the table or token is not exact.</returns>
    public MetadataModuleReferencePhysicalRowIdentity? FindRow(int moduleReferenceToken)
    {
        if (ResultKind != MetadataReferencePhysicalTableResultKind.Exact ||
            !CanonicalReplayEncoding.IsMetadataTokenForTable(moduleReferenceToken, 0x1A))
        {
            return null;
        }
        var rowId = CanonicalReplayEncoding.MetadataTokenRowId(moduleReferenceToken);
        return rowId > 0 && rowId <= rows.Length ? rows[rowId - 1] : null;
    }

    /// <summary>Tests canonical equality between two complete physical ModuleRef draft tables.</summary>
    /// <param name="other">The other draft table.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(MetadataModuleReferencePhysicalTableCatalogIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests complete physical ModuleRef draft-table equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a table with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataModuleReferencePhysicalTableCatalogIdentity);

    /// <summary>Computes a deterministic hash code from fixed-reference canonical draft content.</summary>
    /// <returns>A hash code for this complete physical ModuleRef draft table.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static bool OwnsRowMintCapability(object? capability) => ReferenceEquals(capability, RowMintCapability);
}
