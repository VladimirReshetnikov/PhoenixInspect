using System.Collections.Immutable;
using System.Reflection;
using Interpreter.Core.Abstractions;

namespace Interpreter.Product.DumpQuery;

/// <summary>Classifies one complete source-anchored NestedClass-table draft proof.</summary>
/// <remarks>
/// The draft discriminator separates a complete exact parent map from an acquisition stop and contradictory physical
/// evidence. It is not a compatibility commitment.
/// </remarks>
public enum MetadataNestedClassTableResultKind
{
    /// <summary>The complete physical table and its derived parent map are exact.</summary>
    Exact = 1,

    /// <summary>The complete table was unavailable or an operation bound was reached.</summary>
    NonExact = 2,

    /// <summary>The supplied complete-table claim contradicts source ends or physical row invariants.</summary>
    Invalid = 3,
}

/// <summary>Identifies the typed disposition of one NestedClass-table draft proof.</summary>
/// <remarks>
/// The draft issue remains explicit when no relation prefix is exposed, so later composition never has to infer why
/// the complete parent map was unavailable.
/// </remarks>
public enum MetadataNestedClassTableIssue
{
    /// <summary>No issue applies to an exact complete parent map.</summary>
    None = 0,

    /// <summary>The exact TypeDef-table prerequisite stopped without a complete row catalog.</summary>
    TypeDefinitionTableNonExact = 1,

    /// <summary>The supplied TypeDef-table prerequisite is itself contradictory.</summary>
    TypeDefinitionTableInvalid = 2,

    /// <summary>The TypeDef catalog and NestedClass operation name different exact source ends.</summary>
    TypeDefinitionSourceMismatch = 3,

    /// <summary>The exact source NestedClass table crossed the admitted complete row count.</summary>
    TableRowBoundReached = 4,

    /// <summary>The physical NestedClass table was unavailable or stopped before its exact source end.</summary>
    TableIncomplete = 5,

    /// <summary>The supplied physical row count exceeded the exact NestedClass source end.</summary>
    TableRowCountConflict = 6,

    /// <summary>The supplied NestedClass row tokens did not form the exact physical RID sequence.</summary>
    PhysicalOrderInvalid = 7,

    /// <summary>At least one physical NestedClass row observation belonged to another metadata module.</summary>
    SourceModuleMismatch = 8,

    /// <summary>A nested TypeDef token lay outside the exact source TypeDef table.</summary>
    NestedTypeDefinitionOutOfRange = 9,

    /// <summary>An enclosing TypeDef token lay outside the exact source TypeDef table.</summary>
    EnclosingTypeDefinitionOutOfRange = 10,

    /// <summary>Physical NestedClass rows crossed ascending NestedClass-primary-key order.</summary>
    PrimaryKeySortInvalid = 11,

    /// <summary>More than one physical row claimed a parent for the same nested TypeDef.</summary>
    DuplicateNestedType = 12,

    /// <summary>A physical row named the same TypeDef as both nested type and enclosing type.</summary>
    SelfNesting = 13,

    /// <summary>A parent-map child did not carry one of the nested TypeAttributes visibility values.</summary>
    NestedVisibilityMismatch = 14,

    /// <summary>A TypeDef with nested visibility had no physical NestedClass parent row.</summary>
    NestedVisibilityRelationMissing = 15,

    /// <summary>The complete parent map contains a cycle.</summary>
    ParentCycleDetected = 16,

    /// <summary>The complete acyclic parent map crossed the admitted nested TypeDef depth.</summary>
    NestingDepthBoundReached = 17,
}

/// <summary>Freezes only the physical columns observed from one NestedClass table row.</summary>
/// <remarks>
/// This sealed draft observation carries the source module, physical row token, and both TypeDef tokens. It contains
/// no caller-asserted parent chain, depth, decoded name, or semantic classification.
/// </remarks>
public sealed class MetadataNestedClassRowObservationIdentity :
    IEquatable<MetadataNestedClassRowObservationIdentity>
{
    private const string CanonicalDomain = "metadata-v2-nestedclass-row-observation";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataNestedClassRowObservationIdentity(
        StaticFieldMetadataModuleIdentity metadataModule,
        int nestedClassRowToken,
        int nestedTypeDefinitionToken,
        int enclosingTypeDefinitionToken)
    {
        MetadataModule = metadataModule;
        NestedClassRowToken = nestedClassRowToken;
        NestedTypeDefinitionToken = nestedTypeDefinitionToken;
        EnclosingTypeDefinitionToken = enclosingTypeDefinitionToken;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteLengthPrefixedBytes(metadataModule.CanonicalBytes.AsSpan());
        writer.WriteInt32(nestedClassRowToken);
        writer.WriteInt32(nestedTypeDefinitionToken);
        writer.WriteInt32(enclosingTypeDefinitionToken);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact metadata module containing the physical row.</summary>
    public StaticFieldMetadataModuleIdentity MetadataModule { get; }

    /// <summary>Gets the exact non-nil NestedClass table token, including its physical RID.</summary>
    public int NestedClassRowToken { get; }

    /// <summary>Gets the exact non-nil nested TypeDef token from the physical row.</summary>
    public int NestedTypeDefinitionToken { get; }

    /// <summary>Gets the exact non-nil enclosing TypeDef token from the physical row.</summary>
    public int EnclosingTypeDefinitionToken { get; }

    /// <summary>Gets a defensive copy of the versioned canonical draft bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one physical-column-only NestedClass row draft observation.</summary>
    /// <param name="metadataModule">The exact metadata module containing the row.</param>
    /// <param name="nestedClassRowToken">The exact non-nil NestedClass table token and RID.</param>
    /// <param name="nestedTypeDefinitionToken">The exact non-nil nested TypeDef token.</param>
    /// <param name="enclosingTypeDefinitionToken">The exact non-nil enclosing TypeDef token.</param>
    /// <returns>A sealed immutable draft observation containing no caller-authored parent chain.</returns>
    public static MetadataNestedClassRowObservationIdentity Create(
        StaticFieldMetadataModuleIdentity metadataModule,
        int nestedClassRowToken,
        int nestedTypeDefinitionToken,
        int enclosingTypeDefinitionToken)
    {
        ArgumentNullException.ThrowIfNull(metadataModule);
        CanonicalReplayEncoding.ValidateMetadataToken(
            nestedClassRowToken,
            0x29,
            nameof(nestedClassRowToken));
        CanonicalReplayEncoding.ValidateMetadataToken(
            nestedTypeDefinitionToken,
            0x02,
            nameof(nestedTypeDefinitionToken));
        CanonicalReplayEncoding.ValidateMetadataToken(
            enclosingTypeDefinitionToken,
            0x02,
            nameof(enclosingTypeDefinitionToken));
        return new MetadataNestedClassRowObservationIdentity(
            metadataModule,
            nestedClassRowToken,
            nestedTypeDefinitionToken,
            enclosingTypeDefinitionToken);
    }

    /// <summary>Tests canonical equality between two physical NestedClass draft observations.</summary>
    /// <param name="other">The other draft observation.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(MetadataNestedClassRowObservationIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests physical NestedClass draft equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for an observation with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataNestedClassRowObservationIdentity);

    /// <summary>Computes a hash code from the immutable physical NestedClass draft bytes.</summary>
    /// <returns>A deterministic hash code for canonical draft content.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Freezes one catalog-derived NestedClass parent relation and its complete nesting depth.</summary>
/// <remarks>
/// This sealed draft identity can be minted only by a complete exact NestedClass catalog. Both TypeDef rows come from
/// that catalog's exact TypeDef prerequisite, and the depth is derived from the complete parent map.
/// </remarks>
public sealed class MetadataNestedClassRelationIdentity : IEquatable<MetadataNestedClassRelationIdentity>
{
    private const string CanonicalDomain = "metadata-v2-nestedclass-relation";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataNestedClassRelationIdentity(
        MetadataNestedClassRowObservationIdentity observation,
        MetadataTypeDefinitionTableRowIdentity nestedTypeDefinition,
        MetadataTypeDefinitionTableRowIdentity enclosingTypeDefinition,
        int nestingDepth)
    {
        Observation = observation;
        NestedTypeDefinition = nestedTypeDefinition;
        EnclosingTypeDefinition = enclosingTypeDefinition;
        NestingDepth = nestingDepth;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteLengthPrefixedBytes(observation.CanonicalBytes.AsSpan());
        writer.WriteLengthPrefixedBytes(nestedTypeDefinition.CanonicalBytes.AsSpan());
        writer.WriteLengthPrefixedBytes(enclosingTypeDefinition.CanonicalBytes.AsSpan());
        writer.WriteInt32(nestingDepth);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the unchanged physical NestedClass row draft observation.</summary>
    public MetadataNestedClassRowObservationIdentity Observation { get; }

    /// <summary>Gets the exact nested TypeDef row selected from the correlated complete TypeDef catalog.</summary>
    public MetadataTypeDefinitionTableRowIdentity NestedTypeDefinition { get; }

    /// <summary>Gets the exact enclosing TypeDef row selected from the correlated complete TypeDef catalog.</summary>
    public MetadataTypeDefinitionTableRowIdentity EnclosingTypeDefinition { get; }

    /// <summary>Gets the complete parent-link count from this nested TypeDef to its top-level root.</summary>
    public int NestingDepth { get; }

    /// <summary>Gets a defensive copy of the versioned canonical draft bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Tests canonical equality between two catalog-derived NestedClass draft relations.</summary>
    /// <param name="other">The other derived draft relation.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(MetadataNestedClassRelationIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests catalog-derived NestedClass draft equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a relation with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataNestedClassRelationIdentity);

    /// <summary>Computes a hash code from the immutable catalog-derived NestedClass draft bytes.</summary>
    /// <returns>A deterministic hash code for canonical draft content.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static MetadataNestedClassRelationIdentity Create(
        object mintCapability,
        MetadataNestedClassRowObservationIdentity observation,
        MetadataTypeDefinitionTableRowIdentity nestedTypeDefinition,
        MetadataTypeDefinitionTableRowIdentity enclosingTypeDefinition,
        int nestingDepth)
    {
        if (!MetadataNestedClassTableCatalogIdentity.OwnsRelationMintCapability(mintCapability))
        {
            throw new ArgumentException(
                "A derived NestedClass relation requires the creating catalog's private mint capability.",
                nameof(mintCapability));
        }

        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(nestedTypeDefinition);
        ArgumentNullException.ThrowIfNull(enclosingTypeDefinition);
        if (nestingDepth <= 0 || nestingDepth > StaticFieldV2Limits.MaximumNestedTypeDefinitionDepth)
        {
            throw new ArgumentOutOfRangeException(nameof(nestingDepth));
        }

        return new MetadataNestedClassRelationIdentity(
            observation,
            nestedTypeDefinition,
            enclosingTypeDefinition,
            nestingDepth);
    }
}

/// <summary>Freezes one complete source-anchored NestedClass table and its derived parent map.</summary>
/// <remarks>
/// This sealed draft catalog requires an exact correlated TypeDef catalog and exact physical NestedClass RID coverage.
/// Incomplete and bounded acquisition retains no relation prefix; contradictory evidence retains no parent facts.
/// It is a prerequisite for later TypeDef authority and is not itself a final type identity commitment.
/// </remarks>
public sealed class MetadataNestedClassTableCatalogIdentity :
    IEquatable<MetadataNestedClassTableCatalogIdentity>
{
    private const string CanonicalDomain = "metadata-v2-nestedclass-table-catalog";
    private const int CanonicalSchemaVersion = 1;
    private static readonly object RelationMintCapability = new();
    private readonly ImmutableArray<MetadataNestedClassRelationIdentity> relations;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataNestedClassTableCatalogIdentity(
        MetadataNestedClassTableResultKind resultKind,
        MetadataNestedClassTableIssue issue,
        MetadataSourceEndIdentity sourceEnds,
        MetadataTypeDefinitionTableCatalogIdentity typeDefinitionCatalog,
        ImmutableArray<MetadataNestedClassRelationIdentity> relations,
        EvaluationDeterministicBound? reachedBound,
        int observedCount)
    {
        ResultKind = resultKind;
        Issue = issue;
        SourceEnds = sourceEnds;
        TypeDefinitionCatalog = typeDefinitionCatalog;
        this.relations = relations;
        ReachedBound = reachedBound;
        ObservedCount = observedCount;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32((int)resultKind);
        writer.WriteInt32((int)issue);
        writer.WriteLengthPrefixedBytes(sourceEnds.CanonicalBytes.AsSpan());
        writer.WriteLengthPrefixedBytes(typeDefinitionCatalog.CanonicalBytes.AsSpan());
        ExpressionV2ContractEncoding.WriteCanonicalArray(
            writer,
            relations,
            static relation => relation.CanonicalBytes);
        WriteOptionalBound(writer, reachedBound);
        writer.WriteInt32(observedCount);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets whether the complete draft parent-map proof is exact, non-exact, or invalid.</summary>
    public MetadataNestedClassTableResultKind ResultKind { get; }

    /// <summary>Gets the typed draft table issue, or none for an exact result.</summary>
    public MetadataNestedClassTableIssue Issue { get; }

    /// <summary>Gets the exact source ends against which complete physical coverage was checked.</summary>
    public MetadataSourceEndIdentity SourceEnds { get; }

    /// <summary>Gets the correlated TypeDef-table draft prerequisite retained by this outcome.</summary>
    public MetadataTypeDefinitionTableCatalogIdentity TypeDefinitionCatalog { get; }

    /// <summary>Gets a defensive copy of exact parent relations, or an initialized empty array otherwise.</summary>
    public ImmutableArray<MetadataNestedClassRelationIdentity> Relations =>
        ExpressionV2ContractEncoding.Copy(relations);

    /// <summary>Gets the operation bound reached only by a prefix-free non-exact draft outcome.</summary>
    public EvaluationDeterministicBound? ReachedBound { get; }

    /// <summary>Gets the issue-related cap-plus-one, supplied-row, prerequisite, or depth count.</summary>
    public int ObservedCount { get; }

    /// <summary>Gets a defensive copy of the versioned canonical draft bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one complete source-anchored NestedClass-table draft proof.</summary>
    /// <param name="sourceEnds">The exact table ends for the source metadata module.</param>
    /// <param name="typeDefinitionCatalog">The complete TypeDef-table prerequisite for the same source ends.</param>
    /// <param name="observations">
    /// Every physical NestedClass row in RID order; a default array denotes unavailable complete acquisition.
    /// </param>
    /// <returns>An exact complete parent map, a prefix-free non-exact stop, or a factless invalid draft outcome.</returns>
    public static MetadataNestedClassTableCatalogIdentity Create(
        MetadataSourceEndIdentity sourceEnds,
        MetadataTypeDefinitionTableCatalogIdentity typeDefinitionCatalog,
        ImmutableArray<MetadataNestedClassRowObservationIdentity> observations)
    {
        ArgumentNullException.ThrowIfNull(sourceEnds);
        ArgumentNullException.ThrowIfNull(typeDefinitionCatalog);

        if (!sourceEnds.Equals(typeDefinitionCatalog.SourceEnds))
        {
            return Invalid(
                sourceEnds,
                typeDefinitionCatalog,
                MetadataNestedClassTableIssue.TypeDefinitionSourceMismatch,
                observations.IsDefault ? 0 : observations.Length);
        }

        if (typeDefinitionCatalog.ResultKind == MetadataTypeDefinitionTableResultKind.Invalid)
        {
            return Invalid(
                sourceEnds,
                typeDefinitionCatalog,
                MetadataNestedClassTableIssue.TypeDefinitionTableInvalid,
                typeDefinitionCatalog.ObservedCount);
        }

        if (typeDefinitionCatalog.ResultKind != MetadataTypeDefinitionTableResultKind.Exact)
        {
            return NonExact(
                sourceEnds,
                typeDefinitionCatalog,
                MetadataNestedClassTableIssue.TypeDefinitionTableNonExact,
                typeDefinitionCatalog.ReachedBound,
                typeDefinitionCatalog.ObservedCount);
        }

        var sourceCount = sourceEnds.NestedClassRowCount;
        if (sourceCount > StaticFieldV2Limits.MaximumNestedClassRowCount)
        {
            return NonExact(
                sourceEnds,
                typeDefinitionCatalog,
                MetadataNestedClassTableIssue.TableRowBoundReached,
                new EvaluationDeterministicBound(
                    ExpressionV2ContractLimits.NestedClassRowCountBoundName,
                    StaticFieldV2Limits.MaximumNestedClassRowCount),
                StaticFieldV2Limits.MaximumNestedClassRowCount + 1);
        }

        if (observations.IsDefault || observations.Length < sourceCount)
        {
            return NonExact(
                sourceEnds,
                typeDefinitionCatalog,
                MetadataNestedClassTableIssue.TableIncomplete,
                null,
                observations.IsDefault ? 0 : observations.Length);
        }

        if (observations.Length > sourceCount)
        {
            return Invalid(
                sourceEnds,
                typeDefinitionCatalog,
                MetadataNestedClassTableIssue.TableRowCountConflict,
                observations.Length);
        }

        var copied = ImmutableArray.CreateRange(observations);
        var typeDefinitions = typeDefinitionCatalog.Rows;
        var typeDefinitionsByToken = typeDefinitions.ToDictionary(
            static row => row.TypeDefinitionToken,
            static row => row);
        var parentByNestedToken = new Dictionary<int, int>();
        var previousNestedRowId = 0;
        for (var index = 0; index < copied.Length; index++)
        {
            var observation = copied[index];
            if (observation is null ||
                observation.NestedClassRowToken != (0x2900_0000 | checked(index + 1)))
            {
                return Invalid(
                    sourceEnds,
                    typeDefinitionCatalog,
                    MetadataNestedClassTableIssue.PhysicalOrderInvalid,
                    copied.Length);
            }

            if (!observation.MetadataModule.Equals(sourceEnds.SourceModule))
            {
                return Invalid(
                    sourceEnds,
                    typeDefinitionCatalog,
                    MetadataNestedClassTableIssue.SourceModuleMismatch,
                    copied.Length);
            }

            if (!sourceEnds.ContainsTypeDefinitionToken(observation.NestedTypeDefinitionToken))
            {
                return Invalid(
                    sourceEnds,
                    typeDefinitionCatalog,
                    MetadataNestedClassTableIssue.NestedTypeDefinitionOutOfRange,
                    copied.Length);
            }

            if (!sourceEnds.ContainsTypeDefinitionToken(observation.EnclosingTypeDefinitionToken))
            {
                return Invalid(
                    sourceEnds,
                    typeDefinitionCatalog,
                    MetadataNestedClassTableIssue.EnclosingTypeDefinitionOutOfRange,
                    copied.Length);
            }

            if (observation.NestedTypeDefinitionToken == observation.EnclosingTypeDefinitionToken)
            {
                return Invalid(
                    sourceEnds,
                    typeDefinitionCatalog,
                    MetadataNestedClassTableIssue.SelfNesting,
                    copied.Length);
            }

            if (!parentByNestedToken.TryAdd(
                    observation.NestedTypeDefinitionToken,
                    observation.EnclosingTypeDefinitionToken))
            {
                return Invalid(
                    sourceEnds,
                    typeDefinitionCatalog,
                    MetadataNestedClassTableIssue.DuplicateNestedType,
                    copied.Length);
            }

            var nestedRowId = CanonicalReplayEncoding.MetadataTokenRowId(
                observation.NestedTypeDefinitionToken);
            if (nestedRowId < previousNestedRowId)
            {
                return Invalid(
                    sourceEnds,
                    typeDefinitionCatalog,
                    MetadataNestedClassTableIssue.PrimaryKeySortInvalid,
                    copied.Length);
            }

            previousNestedRowId = nestedRowId;
        }

        foreach (var observation in copied)
        {
            var nestedType = typeDefinitionsByToken[observation.NestedTypeDefinitionToken];
            if (!HasNestedVisibility(nestedType.Observation.TypeAttributes))
            {
                return Invalid(
                    sourceEnds,
                    typeDefinitionCatalog,
                    MetadataNestedClassTableIssue.NestedVisibilityMismatch,
                    copied.Length);
            }
        }

        foreach (var typeDefinition in typeDefinitions)
        {
            if (HasNestedVisibility(typeDefinition.Observation.TypeAttributes) &&
                !parentByNestedToken.ContainsKey(typeDefinition.TypeDefinitionToken))
            {
                return Invalid(
                    sourceEnds,
                    typeDefinitionCatalog,
                    MetadataNestedClassTableIssue.NestedVisibilityRelationMissing,
                    copied.Length);
            }
        }

        if (ContainsCycle(parentByNestedToken))
        {
            return Invalid(
                sourceEnds,
                typeDefinitionCatalog,
                MetadataNestedClassTableIssue.ParentCycleDetected,
                copied.Length);
        }

        var depths = DeriveDepths(parentByNestedToken);
        var maximumDepth = depths.Count == 0 ? 0 : depths.Values.Max();
        if (maximumDepth > StaticFieldV2Limits.MaximumNestedTypeDefinitionDepth)
        {
            return NonExact(
                sourceEnds,
                typeDefinitionCatalog,
                MetadataNestedClassTableIssue.NestingDepthBoundReached,
                new EvaluationDeterministicBound(
                    ExpressionV2ContractLimits.NestedTypeDefinitionDepthBoundName,
                    StaticFieldV2Limits.MaximumNestedTypeDefinitionDepth),
                StaticFieldV2Limits.MaximumNestedTypeDefinitionDepth + 1);
        }

        var relations = ImmutableArray.CreateBuilder<MetadataNestedClassRelationIdentity>(copied.Length);
        foreach (var observation in copied)
        {
            relations.Add(MetadataNestedClassRelationIdentity.Create(
                RelationMintCapability,
                observation,
                typeDefinitionsByToken[observation.NestedTypeDefinitionToken],
                typeDefinitionsByToken[observation.EnclosingTypeDefinitionToken],
                depths[observation.NestedTypeDefinitionToken]));
        }

        return new MetadataNestedClassTableCatalogIdentity(
            MetadataNestedClassTableResultKind.Exact,
            MetadataNestedClassTableIssue.None,
            sourceEnds,
            typeDefinitionCatalog,
            relations.MoveToImmutable(),
            null,
            0);
    }

    /// <summary>Tests canonical equality between two complete NestedClass-table draft proofs.</summary>
    /// <param name="other">The other draft catalog.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(MetadataNestedClassTableCatalogIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests complete NestedClass-table draft equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a catalog with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataNestedClassTableCatalogIdentity);

    /// <summary>Computes a hash code from the immutable complete-table draft bytes.</summary>
    /// <returns>A deterministic hash code for canonical draft content.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal MetadataNestedClassRelationIdentity? ExactRelationOrDefault(int nestedTypeDefinitionToken)
    {
        if (ResultKind != MetadataNestedClassTableResultKind.Exact ||
            !CanonicalReplayEncoding.IsMetadataTokenForTable(nestedTypeDefinitionToken, 0x02))
        {
            return null;
        }

        return relations.FirstOrDefault(
            relation => relation.NestedTypeDefinition.TypeDefinitionToken == nestedTypeDefinitionToken);
    }

    internal static bool OwnsRelationMintCapability(object? capability) =>
        ReferenceEquals(capability, RelationMintCapability);

    private static bool HasNestedVisibility(int typeAttributes)
    {
        var visibility = (TypeAttributes)typeAttributes & TypeAttributes.VisibilityMask;
        return visibility is TypeAttributes.NestedPublic or
            TypeAttributes.NestedPrivate or
            TypeAttributes.NestedFamily or
            TypeAttributes.NestedAssembly or
            TypeAttributes.NestedFamANDAssem or
            TypeAttributes.NestedFamORAssem;
    }

    private static bool ContainsCycle(IReadOnlyDictionary<int, int> parentByNestedToken)
    {
        var states = new Dictionary<int, byte>();
        foreach (var start in parentByNestedToken.Keys)
        {
            if (states.TryGetValue(start, out var startState) && startState == 2)
            {
                continue;
            }

            var path = new List<int>();
            var current = start;
            while (parentByNestedToken.TryGetValue(current, out var parent))
            {
                if (states.TryGetValue(current, out var state))
                {
                    if (state == 1)
                    {
                        return true;
                    }
                    break;
                }

                states[current] = 1;
                path.Add(current);
                current = parent;
            }

            foreach (var token in path)
            {
                states[token] = 2;
            }
        }

        return false;
    }

    private static Dictionary<int, int> DeriveDepths(IReadOnlyDictionary<int, int> parentByNestedToken)
    {
        var depths = new Dictionary<int, int>();
        foreach (var start in parentByNestedToken.Keys)
        {
            if (depths.ContainsKey(start))
            {
                continue;
            }

            var path = new List<int>();
            var current = start;
            while (!depths.ContainsKey(current) && parentByNestedToken.TryGetValue(current, out var parent))
            {
                path.Add(current);
                current = parent;
            }

            var depth = depths.GetValueOrDefault(current);
            for (var index = path.Count - 1; index >= 0; index--)
            {
                depth = checked(depth + 1);
                depths[path[index]] = depth;
            }
        }

        return depths;
    }

    private static void WriteOptionalBound(
        CanonicalReplayEncoding.Writer writer,
        EvaluationDeterministicBound? bound)
    {
        writer.WriteBoolean(bound is not null);
        if (bound is not null)
        {
            writer.WriteString(bound.Name);
            writer.WriteInt64(bound.Value);
        }
    }

    private static MetadataNestedClassTableCatalogIdentity NonExact(
        MetadataSourceEndIdentity sourceEnds,
        MetadataTypeDefinitionTableCatalogIdentity typeDefinitionCatalog,
        MetadataNestedClassTableIssue issue,
        EvaluationDeterministicBound? reachedBound,
        int observedCount) =>
        new(
            MetadataNestedClassTableResultKind.NonExact,
            issue,
            sourceEnds,
            typeDefinitionCatalog,
            ImmutableArray<MetadataNestedClassRelationIdentity>.Empty,
            reachedBound,
            observedCount);

    private static MetadataNestedClassTableCatalogIdentity Invalid(
        MetadataSourceEndIdentity sourceEnds,
        MetadataTypeDefinitionTableCatalogIdentity typeDefinitionCatalog,
        MetadataNestedClassTableIssue issue,
        int observedCount) =>
        new(
            MetadataNestedClassTableResultKind.Invalid,
            issue,
            sourceEnds,
            typeDefinitionCatalog,
            ImmutableArray<MetadataNestedClassRelationIdentity>.Empty,
            null,
            observedCount);
}
