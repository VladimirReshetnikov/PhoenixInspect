using System.Collections.Immutable;
using PhoenixInspect.Core.Abstractions;

namespace PhoenixInspect.Product.DumpQuery;

/// <summary>Classifies one complete authority-to-compiler-name catalog projection.</summary>
/// <remarks>
/// Exact means every authority-issued TypeDef received one mapping outcome. An individual mapping may remain
/// non-exact when its nested physical arities underflow; that row-level disposition does not make the complete
/// projection partial.
/// </remarks>
public enum MetadataCompilerNameMappingCatalogResultKind
{
    /// <summary>The exact definition authority was projected completely in TypeDef RID order.</summary>
    Exact = 1,

    /// <summary>The retained definition authority stopped before it could expose any TypeDef rows.</summary>
    NonExact = 2,

    /// <summary>The retained definition authority contains contradictory complete evidence.</summary>
    Invalid = 3,
}

/// <summary>Identifies the typed prerequisite issue of one compiler-name mapping catalog.</summary>
/// <remarks>
/// The detailed physical issue remains available through the retained definition authority. These values distinguish
/// whether no mapping prefix exists because that prerequisite was non-exact or invalid.
/// </remarks>
public enum MetadataCompilerNameMappingCatalogIssue
{
    /// <summary>No catalog-level issue applies to an exact complete projection.</summary>
    None = 0,

    /// <summary>The definition-authority prerequisite was non-exact.</summary>
    DefinitionAuthorityNonExact = 1,

    /// <summary>The definition-authority prerequisite was invalid.</summary>
    DefinitionAuthorityInvalid = 2,
}

/// <summary>Freezes the complete compiler-name mapping catalog for one definition authority.</summary>
/// <remarks>
/// This sealed catalog is the sole issuer of authority-bound compiler-name mapping identities. It selects every
/// target and immediate parent from one exact definition-authority catalog, emits mappings in TypeDef RID order, and
/// exposes no mapping prefix when that prerequisite is non-exact or invalid. The physical definition authority remains
/// a separate canonical identity.
/// </remarks>
public sealed class MetadataCompilerNameMappingCatalogIdentity :
    IEquatable<MetadataCompilerNameMappingCatalogIdentity>
{
    private const string CanonicalDomain = "metadata-v2-compiler-name-mapping-catalog";
    private const int CanonicalSchemaVersion = 1;
    private static readonly object MappingMintCapability = new();
    private readonly ImmutableArray<MetadataCompilerNameMappingIdentity> mappings;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataCompilerNameMappingCatalogIdentity(
        MetadataCompilerNameMappingCatalogResultKind resultKind,
        MetadataCompilerNameMappingCatalogIssue issue,
        MetadataDefinitionAuthorityCatalogIdentity definitionAuthority,
        ImmutableArray<MetadataCompilerNameMappingIdentity> mappings)
    {
        ResultKind = resultKind;
        Issue = issue;
        DefinitionAuthority = definitionAuthority;
        this.mappings = ExpressionV2ContractEncoding.Copy(mappings);

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32((int)resultKind);
        writer.WriteInt32((int)issue);
        writer.WriteLengthPrefixedBytes(definitionAuthority.CanonicalBytes.AsSpan());
        ExpressionV2ContractEncoding.WriteCanonicalArray(
            writer,
            mappings,
            static mapping => mapping.CanonicalBytes);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets whether this complete projection is exact, non-exact, or invalid.</summary>
    public MetadataCompilerNameMappingCatalogResultKind ResultKind { get; }

    /// <summary>Gets the typed catalog-level issue, or none for an exact complete projection.</summary>
    public MetadataCompilerNameMappingCatalogIssue Issue { get; }

    /// <summary>Gets the complete definition-authority prerequisite retained by this catalog.</summary>
    public MetadataDefinitionAuthorityCatalogIdentity DefinitionAuthority { get; }

    /// <summary>Gets the detailed issue retained by the definition-authority prerequisite.</summary>
    public MetadataDefinitionAuthorityIssue DefinitionAuthorityIssue => DefinitionAuthority.Issue;

    /// <summary>Gets the prerequisite operation bound for a non-exact outcome, otherwise null.</summary>
    public EvaluationDeterministicBound? ReachedBound => DefinitionAuthority.ReachedBound;

    /// <summary>Gets the prerequisite issue-related observation count.</summary>
    public int ObservedCount => DefinitionAuthority.ObservedCount;

    /// <summary>Gets the prerequisite issue-related metadata token, otherwise null.</summary>
    public int? RelatedMetadataToken => DefinitionAuthority.RelatedMetadataToken;

    /// <summary>
    /// Gets a defensive TypeDef-RID-order copy of all mapping outcomes, or an empty array for a prerequisite stop.
    /// </summary>
    public ImmutableArray<MetadataCompilerNameMappingIdentity> Mappings =>
        ExpressionV2ContractEncoding.Copy(mappings);

    /// <summary>Gets a defensive copy of the versioned canonical catalog bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical catalog bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one complete authority-to-compiler-name catalog projection.</summary>
    /// <param name="definitionAuthority">
    /// The complete physical definition-authority outcome whose exact TypeDef rows are the sole mapping inputs.
    /// </param>
    /// <returns>
    /// An exact complete mapping catalog, or a prefix-free non-exact or invalid catalog mirroring the prerequisite.
    /// </returns>
    public static MetadataCompilerNameMappingCatalogIdentity Create(
        MetadataDefinitionAuthorityCatalogIdentity definitionAuthority)
    {
        ArgumentNullException.ThrowIfNull(definitionAuthority);
        if (definitionAuthority.ResultKind == MetadataDefinitionAuthorityResultKind.NonExact)
        {
            return Stopped(
                MetadataCompilerNameMappingCatalogResultKind.NonExact,
                MetadataCompilerNameMappingCatalogIssue.DefinitionAuthorityNonExact,
                definitionAuthority);
        }
        if (definitionAuthority.ResultKind == MetadataDefinitionAuthorityResultKind.Invalid)
        {
            return Stopped(
                MetadataCompilerNameMappingCatalogResultKind.Invalid,
                MetadataCompilerNameMappingCatalogIssue.DefinitionAuthorityInvalid,
                definitionAuthority);
        }

        var typeDefinitions = definitionAuthority.TypeDefinitions;
        var builder = ImmutableArray.CreateBuilder<MetadataCompilerNameMappingIdentity>(typeDefinitions.Length);
        foreach (var typeDefinition in typeDefinitions)
        {
            MetadataTypeDefinitionAuthorityIdentity? enclosingTypeDefinition = null;
            if (typeDefinition.EnclosingTypeDefinitionToken is { } enclosingToken)
            {
                enclosingTypeDefinition = definitionAuthority.ExactTypeDefinitionOrDefault(enclosingToken);
                if (enclosingTypeDefinition is null)
                {
                    throw new InvalidOperationException(
                        "An exact definition authority must resolve every nested TypeDef's immediate parent.");
                }
            }

            builder.Add(MetadataCompilerNameMappingIdentity.Create(
                MappingMintCapability,
                typeDefinition,
                enclosingTypeDefinition));
        }

        return new MetadataCompilerNameMappingCatalogIdentity(
            MetadataCompilerNameMappingCatalogResultKind.Exact,
            MetadataCompilerNameMappingCatalogIssue.None,
            definitionAuthority,
            builder.MoveToImmutable());
    }

    /// <summary>Tests canonical equality between two complete compiler-name mapping catalogs.</summary>
    /// <param name="other">The other catalog.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(MetadataCompilerNameMappingCatalogIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests compiler-name mapping catalog equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a catalog with identical canonical content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataCompilerNameMappingCatalogIdentity);

    /// <summary>Computes a deterministic hash code from the immutable compiler-name mapping catalog.</summary>
    /// <returns>A hash code for this canonical catalog.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal MetadataCompilerNameMappingIdentity? CompleteMappingOrDefault(int typeDefinitionToken)
    {
        if (ResultKind != MetadataCompilerNameMappingCatalogResultKind.Exact ||
            !CanonicalReplayEncoding.IsMetadataTokenForTable(typeDefinitionToken, 0x02))
        {
            return null;
        }

        var rowId = CanonicalReplayEncoding.MetadataTokenRowId(typeDefinitionToken);
        return rowId > 0 && rowId <= mappings.Length
            ? mappings[rowId - 1]
            : null;
    }

    internal static bool OwnsMappingMintCapability(object? capability) =>
        ReferenceEquals(capability, MappingMintCapability);

    private static MetadataCompilerNameMappingCatalogIdentity Stopped(
        MetadataCompilerNameMappingCatalogResultKind resultKind,
        MetadataCompilerNameMappingCatalogIssue issue,
        MetadataDefinitionAuthorityCatalogIdentity definitionAuthority) =>
        new(
            resultKind,
            issue,
            definitionAuthority,
            ImmutableArray<MetadataCompilerNameMappingIdentity>.Empty);
}
