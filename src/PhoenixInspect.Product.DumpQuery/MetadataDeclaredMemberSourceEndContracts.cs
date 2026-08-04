using System.Collections.Immutable;
using PhoenixInspect.Core.Abstractions;

namespace PhoenixInspect.Product.DumpQuery;

/// <summary>Names which HasConstant parent table owns one physical Constant row.</summary>
/// <remarks>
/// ECMA-335 II.24.2.6 encodes Constant.Parent as a HasConstant coded index over exactly these three tables. The
/// discriminator is a pure decoding of the parent token's table byte and asserts nothing about the row's value.
/// </remarks>
public enum MetadataConstantParentKind
{
    /// <summary>The Constant row belongs to a FieldDef (0x04) row.</summary>
    FieldDefinition = 1,

    /// <summary>The Constant row belongs to a Param (0x08) row.</summary>
    ParameterDefinition = 2,

    /// <summary>The Constant row belongs to a Property (0x17) row.</summary>
    PropertyDefinition = 3,
}

/// <summary>Extends one exact metadata source end with the declaration-side tables.</summary>
/// <remarks>
/// The existing source-end digest remains authoritative for FieldDef and Param. Property, Constant, PropertyMap, and
/// PropertyPtr counts are projected from the same retained exhaustive module-search fact, so this extension can never
/// describe a different image than the source ends it binds to — it carries their digest rather than a second copy of
/// their evidence.
/// <para>
/// Three of the four extended tables are counted but never enumerated, because the shared metadata reader projects no
/// rows for Constant (0x0B), PropertyMap (0x15), or PropertyPtr (0x16). That is precisely why their counts matter
/// here: a catalog assembled from the parent side proves it collected every row by agreeing with a count it did not
/// produce, which no self-consistent collection could fake.
/// </para>
/// </remarks>
public sealed class MetadataDeclaredMemberSourceEndIdentity : IEquatable<MetadataDeclaredMemberSourceEndIdentity>
{
    private const string CanonicalDomain = "metadata-v2-declared-member-source-end";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataDeclaredMemberSourceEndIdentity(
        MetadataSourceEndIdentity definitionSourceEnds,
        int propertyRowCount,
        int constantRowCount,
        int propertyMapRowCount,
        int propertyPointerRowCount)
    {
        DefinitionSourceEnds = definitionSourceEnds;
        PropertyRowCount = propertyRowCount;
        ConstantRowCount = constantRowCount;
        PropertyMapRowCount = propertyMapRowCount;
        PropertyPointerRowCount = propertyPointerRowCount;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteSha256(definitionSourceEnds.Sha256, nameof(definitionSourceEnds));
        writer.WriteInt32(definitionSourceEnds.FieldDefinitionRowCount);
        writer.WriteInt32(definitionSourceEnds.ParameterDefinitionRowCount);
        writer.WriteInt32(propertyRowCount);
        writer.WriteInt32(constantRowCount);
        writer.WriteInt32(propertyMapRowCount);
        writer.WriteInt32(propertyPointerRowCount);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact landed source ends this declaration-side extension is bound to.</summary>
    public MetadataSourceEndIdentity DefinitionSourceEnds { get; }

    /// <summary>Gets the exact metadata module shared by every declaration-side table.</summary>
    public StaticFieldMetadataModuleIdentity SourceModule => DefinitionSourceEnds.SourceModule;

    /// <summary>Gets the exact physical FieldDef table row count.</summary>
    public int FieldDefinitionRowCount => DefinitionSourceEnds.FieldDefinitionRowCount;

    /// <summary>Gets the exact physical Param table row count.</summary>
    public int ParameterDefinitionRowCount => DefinitionSourceEnds.ParameterDefinitionRowCount;

    /// <summary>Gets the exact physical Property table row count.</summary>
    public int PropertyRowCount { get; }

    /// <summary>Gets the exact physical Constant table row count.</summary>
    public int ConstantRowCount { get; }

    /// <summary>Gets the exact physical PropertyMap table row count.</summary>
    public int PropertyMapRowCount { get; }

    /// <summary>Gets the exact physical PropertyPtr table row count.</summary>
    public int PropertyPointerRowCount { get; }

    /// <summary>Gets a defensive copy of the fixed-reference canonical source-end bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical source-end bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one exact declaration-side source-end extension.</summary>
    /// <param name="definitionSourceEnds">
    /// The exact landed source ends whose retained exhaustive module-search fact observed every projected count,
    /// including the all-or-nothing declaration-side row-count bundle.
    /// </param>
    /// <returns>A fixed-reference identity covering FieldDef, Param, Property, Constant, PropertyMap, and PropertyPtr.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="definitionSourceEnds"/> is null.</exception>
    /// <exception cref="ArgumentException">The retained module-search fact is not exact or observed no declaration-side counts.</exception>
    public static MetadataDeclaredMemberSourceEndIdentity Create(MetadataSourceEndIdentity definitionSourceEnds)
    {
        ArgumentNullException.ThrowIfNull(definitionSourceEnds);
        var fact = definitionSourceEnds.SourceModuleFact;
        if (fact.Status != StaticFieldModuleSearchStatus.Exact ||
            fact.PropertyDefinitionRowCount is not { } propertyRowCount ||
            fact.DeclaredMemberRowCounts is not { } declaredMembers)
        {
            throw new ArgumentException(
                "Declaration-side source ends require complete row counts from the retained exact module-search fact.",
                nameof(definitionSourceEnds));
        }

        return new MetadataDeclaredMemberSourceEndIdentity(
            definitionSourceEnds,
            propertyRowCount,
            declaredMembers.ConstantRowCount,
            declaredMembers.PropertyMapRowCount,
            declaredMembers.PropertyPointerRowCount);
    }

    /// <summary>Tests whether one token is an in-range HasConstant parent and names which table owns it.</summary>
    /// <param name="metadataToken">The non-nil metadata token to test.</param>
    /// <param name="parentKind">The decoded parent table on success.</param>
    /// <returns><see langword="true"/> only for an in-range token from one of the three HasConstant tables.</returns>
    public bool ContainsHasConstantParentToken(int metadataToken, out MetadataConstantParentKind parentKind)
    {
        var rowId = CanonicalReplayEncoding.MetadataTokenRowId(metadataToken);
        switch (metadataToken & unchecked((int)0xFF00_0000))
        {
            case 0x0400_0000 when rowId > 0 && rowId <= FieldDefinitionRowCount:
                parentKind = MetadataConstantParentKind.FieldDefinition;
                return true;
            case 0x0800_0000 when rowId > 0 && rowId <= ParameterDefinitionRowCount:
                parentKind = MetadataConstantParentKind.ParameterDefinition;
                return true;
            case 0x1700_0000 when rowId > 0 && rowId <= PropertyRowCount:
                parentKind = MetadataConstantParentKind.PropertyDefinition;
                return true;
            default:
                parentKind = default;
                return false;
        }
    }

    /// <summary>Tests whether one token lies inside the exact physical Property table end.</summary>
    /// <param name="metadataToken">The non-nil metadata token to test.</param>
    /// <returns><see langword="true"/> only for an in-range Property token.</returns>
    public bool ContainsPropertyToken(int metadataToken)
    {
        var rowId = CanonicalReplayEncoding.MetadataTokenRowId(metadataToken);
        return rowId > 0 &&
            (metadataToken & unchecked((int)0xFF00_0000)) == 0x1700_0000 &&
            rowId <= PropertyRowCount;
    }

    /// <summary>Tests canonical equality between two exact declaration-side source-end identities.</summary>
    /// <param name="other">The other source-end identity.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(MetadataDeclaredMemberSourceEndIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests declaration-side source-end equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for an identity with identical canonical content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataDeclaredMemberSourceEndIdentity);

    /// <summary>Computes a deterministic hash code from fixed-reference canonical content.</summary>
    /// <returns>A hash code for this declaration-side source-end identity.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}
