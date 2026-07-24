using System.Collections.Immutable;
using PhoenixInspect.Core.Abstractions;

namespace PhoenixInspect.Product.DumpQuery;

/// <summary>Classifies one complete GenericParam proof-stage outcome.</summary>
/// <remarks>The draft result never exposes a usable prefix after incomplete acquisition or a reached bound.</remarks>
public enum MetadataGenericParameterProofResultKind
{
    /// <summary>The complete operation is exact and exposes its normalized result.</summary>
    Exact = 1,
    /// <summary>The complete operation lacks source evidence or observed cap-plus-one.</summary>
    NonExact = 2,
    /// <summary>The complete supplied evidence is contradictory or malformed.</summary>
    Invalid = 3,
}

/// <summary>Identifies the typed issue carried by one GenericParam proof-stage outcome.</summary>
/// <remarks>The draft issue catalog distinguishes absent evidence, bounds, and contradictory physical rows.</remarks>
public enum MetadataGenericParameterProofIssue
{
    /// <summary>No issue applies to an exact result.</summary>
    None = 0,
    /// <summary>A TypeDef or MethodDef declared more than the admitted per-owner arity.</summary>
    OwnerArityBoundReached = 1,
    /// <summary>The complete MethodDef signature failed the shared structural grammar.</summary>
    MethodSignatureInvalid = 2,
    /// <summary>MethodAttributes.Static and the MethodDef signature HASTHIS bit disagree.</summary>
    MethodReceiverMismatch = 3,
    /// <summary>The module-wide GenericParam source end exceeded the declared row cap.</summary>
    TableRowBoundReached = 4,
    /// <summary>The complete module-wide GenericParam row input was not acquired.</summary>
    TableIncomplete = 5,
    /// <summary>The supplied row count exceeds the exact GenericParam source end.</summary>
    TableRowCountConflict = 6,
    /// <summary>The supplied GenericParam tokens are not the complete physical RID sequence.</summary>
    PhysicalOrderInvalid = 7,
    /// <summary>A row, declaration, or catalog belongs to a different exact metadata module.</summary>
    SourceModuleMismatch = 8,
    /// <summary>Two physical rows claim the same exact owner and position.</summary>
    DuplicateOwnerPosition = 9,
    /// <summary>The owner declaration is invalid and cannot select rows.</summary>
    OwnerDeclarationInvalid = 10,
    /// <summary>The selected owner rows do not cover every declared position exactly once.</summary>
    OwnerPositionCoverageInvalid = 11,
    /// <summary>At least one selected row lacks a binding observation.</summary>
    BindingIncomplete = 12,
    /// <summary>One physical GenericParam row has multiple binding observations.</summary>
    DuplicateBinding = 13,
    /// <summary>A binding does not name an exact row selected by the owner proof.</summary>
    BindingRowMismatch = 14,
    /// <summary>Physical GenericParam RIDs cross the required TypeOrMethodDef coded-owner order.</summary>
    OwnerSortInvalid = 15,
    /// <summary>An owner token belongs to the exact source module but lies beyond its TypeDef or MethodDef source end.</summary>
    OwnerTokenOutOfRange = 16,
    /// <summary>The complete definition-authority prerequisite stopped before issuing exact rows.</summary>
    DefinitionAuthorityNonExact = 17,
    /// <summary>The complete definition-authority prerequisite retained contradictory evidence.</summary>
    DefinitionAuthorityInvalid = 18,
}

/// <summary>Classifies evaluator admission for one authority-issued GenericParam owner group.</summary>
/// <remarks>
/// This draft discriminator leaves the complete physical group exact when its arity exceeds the current evaluator
/// limit. Admission is a separate disposition and never truncates the authority-issued row set.
/// </remarks>
public enum MetadataGenericParameterOwnerAdmissionKind
{
    /// <summary>The exact declared arity is within the current evaluator limit.</summary>
    Admitted = 1,

    /// <summary>The exact declared arity exceeds the current evaluator limit.</summary>
    ArityBoundReached = 2,
}

/// <summary>Freezes one complete GenericParam owner group issued from definition authority.</summary>
/// <remarks>
/// This sealed draft identity has no public issuer. An exact
/// <see cref="MetadataGenericParameterAuthorityCatalogIdentity"/> alone may mint it from an authority-issued TypeDef
/// or MethodDef and that row's complete Number-ordered GenericParam group. Physical exactness remains independent of
/// evaluator admission.
/// </remarks>
public sealed class MetadataGenericParameterAuthorityOwnerGroupIdentity :
    IEquatable<MetadataGenericParameterAuthorityOwnerGroupIdentity>
{
    private const string CanonicalDomain = "metadata-v2-generic-parameter-authority-owner-group";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<MetadataGenericParameterTableRowIdentity> parameters;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataGenericParameterAuthorityOwnerGroupIdentity(
        MetadataDefinitionAuthorityCatalogIdentity definitionAuthority,
        MetadataGenericParameterOwnerKind ownerKind,
        MetadataTypeDefinitionAuthorityIdentity? typeDefinition,
        MetadataMethodDefinitionAuthorityIdentity? methodDefinition,
        ImmutableArray<MetadataGenericParameterTableRowIdentity> parameters,
        int declaredArity)
    {
        DefinitionAuthority = definitionAuthority;
        OwnerKind = ownerKind;
        TypeDefinition = typeDefinition;
        MethodDefinition = methodDefinition;
        this.parameters = ExpressionV2ContractEncoding.Copy(parameters);
        DeclaredArity = declaredArity;
        AdmissionKind = declaredArity <= StaticFieldV2Limits.MaximumGenericParameterCount
            ? MetadataGenericParameterOwnerAdmissionKind.Admitted
            : MetadataGenericParameterOwnerAdmissionKind.ArityBoundReached;
        ReachedBound = AdmissionKind == MetadataGenericParameterOwnerAdmissionKind.ArityBoundReached
            ? new EvaluationDeterministicBound(
                ExpressionV2ContractLimits.GenericParameterCountBoundName,
                StaticFieldV2Limits.MaximumGenericParameterCount)
            : null;
        ObservedCount = ReachedBound is null
            ? 0
            : StaticFieldV2Limits.MaximumGenericParameterCount + 1;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteSha256(definitionAuthority.Sha256, nameof(definitionAuthority));
        writer.WriteInt32((int)ownerKind);
        writer.WriteBoolean(typeDefinition is not null);
        if (typeDefinition is not null)
        {
            writer.WriteSha256(typeDefinition.Sha256, nameof(typeDefinition));
        }
        writer.WriteBoolean(methodDefinition is not null);
        if (methodDefinition is not null)
        {
            writer.WriteSha256(methodDefinition.Sha256, nameof(methodDefinition));
        }
        ExpressionV2ContractEncoding.WriteCanonicalArray(
            writer,
            parameters,
            static parameter => parameter.CanonicalBytes);
        writer.WriteInt32(declaredArity);
        writer.WriteInt32((int)AdmissionKind);
        ExpressionV2ContractEncoding.WriteOptionalBound(writer, ReachedBound);
        writer.WriteInt32(ObservedCount);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the complete definition authority that issued this draft group.</summary>
    public MetadataDefinitionAuthorityCatalogIdentity DefinitionAuthority { get; }

    /// <summary>Gets whether this exact draft group belongs to a TypeDef or MethodDef authority row.</summary>
    public MetadataGenericParameterOwnerKind OwnerKind { get; }

    /// <summary>Gets the authority-issued TypeDef owner, or null for a MethodDef group.</summary>
    public MetadataTypeDefinitionAuthorityIdentity? TypeDefinition { get; }

    /// <summary>Gets the authority-issued MethodDef owner, or null for a TypeDef group.</summary>
    public MetadataMethodDefinitionAuthorityIdentity? MethodDefinition { get; }

    /// <summary>Gets the exact TypeDef or MethodDef owner token.</summary>
    public int OwnerMetadataToken =>
        TypeDefinition?.TypeDefinitionToken ?? MethodDefinition!.MethodDefinitionToken;

    /// <summary>Gets the exact source ends retained by the issuing definition authority.</summary>
    public MetadataSourceEndIdentity SourceEnds => DefinitionAuthority.SourceEnds;

    /// <summary>Gets a defensive Number-ordered copy of every exact physical row in this draft group.</summary>
    public ImmutableArray<MetadataGenericParameterTableRowIdentity> Parameters =>
        ExpressionV2ContractEncoding.Copy(parameters);

    /// <summary>
    /// Gets the exact declared arity from TypeDef authority or signature-agreed MethodDef authority, including zero.
    /// </summary>
    public int DeclaredArity { get; }

    /// <summary>Gets the evaluator admission disposition without changing physical draft exactness.</summary>
    public MetadataGenericParameterOwnerAdmissionKind AdmissionKind { get; }

    /// <summary>Gets whether the exact draft group is within the current evaluator arity limit.</summary>
    public bool IsAdmitted => AdmissionKind == MetadataGenericParameterOwnerAdmissionKind.Admitted;

    /// <summary>Gets the evaluator arity limit only when the exact draft group exceeds it.</summary>
    public EvaluationDeterministicBound? ReachedBound { get; }

    /// <summary>Gets limit plus one only when the exact draft group exceeds evaluator admission.</summary>
    public int ObservedCount { get; }

    /// <summary>Gets a defensive copy of the versioned canonical draft bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Tests canonical equality between two authority-issued GenericParam draft groups.</summary>
    /// <param name="other">The other authority-issued draft group.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(MetadataGenericParameterAuthorityOwnerGroupIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests authority-issued GenericParam draft group equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for an identity with identical canonical draft content.</returns>
    public override bool Equals(object? obj) =>
        Equals(obj as MetadataGenericParameterAuthorityOwnerGroupIdentity);

    /// <summary>Computes a deterministic hash code from immutable canonical draft content.</summary>
    /// <returns>A hash code for this authority-issued GenericParam draft group.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static MetadataGenericParameterAuthorityOwnerGroupIdentity CreateForTypeDefinition(
        object mintCapability,
        MetadataDefinitionAuthorityCatalogIdentity definitionAuthority,
        MetadataTypeDefinitionAuthorityIdentity typeDefinition)
    {
        if (!MetadataGenericParameterAuthorityCatalogIdentity.OwnsOwnerGroupMintCapability(mintCapability))
        {
            throw new ArgumentException(
                "A TypeDef GenericParam group requires the authority catalog's private mint capability.",
                nameof(mintCapability));
        }

        ArgumentNullException.ThrowIfNull(definitionAuthority);
        ArgumentNullException.ThrowIfNull(typeDefinition);
        return new MetadataGenericParameterAuthorityOwnerGroupIdentity(
            definitionAuthority,
            MetadataGenericParameterOwnerKind.TypeDefinition,
            typeDefinition,
            null,
            typeDefinition.GenericParameters,
            typeDefinition.TotalGenericArity);
    }

    internal static MetadataGenericParameterAuthorityOwnerGroupIdentity CreateForMethodDefinition(
        object mintCapability,
        MetadataDefinitionAuthorityCatalogIdentity definitionAuthority,
        MetadataMethodDefinitionAuthorityIdentity methodDefinition)
    {
        if (!MetadataGenericParameterAuthorityCatalogIdentity.OwnsOwnerGroupMintCapability(mintCapability))
        {
            throw new ArgumentException(
                "A MethodDef GenericParam group requires the authority catalog's private mint capability.",
                nameof(mintCapability));
        }

        ArgumentNullException.ThrowIfNull(definitionAuthority);
        ArgumentNullException.ThrowIfNull(methodDefinition);
        return new MetadataGenericParameterAuthorityOwnerGroupIdentity(
            definitionAuthority,
            MetadataGenericParameterOwnerKind.MethodDefinition,
            null,
            methodDefinition,
            methodDefinition.GenericParameters,
            methodDefinition.DeclaredGenericArity);
    }

    internal ImmutableArray<MetadataGenericParameterTableRowIdentity> ExactParameters => parameters;
}

/// <summary>Freezes every authority-issued TypeDef and MethodDef GenericParam owner group for one source.</summary>
/// <remarks>
/// This sealed draft catalog consumes only definition authority. Exact results issue one group for every definition,
/// including initialized zero-arity groups, in TypeOrMethodDef coded-owner order. Non-exact and invalid prerequisites
/// expose no group prefix.
/// </remarks>
public sealed class MetadataGenericParameterAuthorityCatalogIdentity :
    IEquatable<MetadataGenericParameterAuthorityCatalogIdentity>
{
    private const string CanonicalDomain = "metadata-v2-generic-parameter-authority-catalog";
    private const int CanonicalSchemaVersion = 1;
    private static readonly object OwnerGroupMintCapability = new();
    private readonly ImmutableArray<MetadataGenericParameterAuthorityOwnerGroupIdentity> groups;
    private readonly ImmutableArray<MetadataGenericParameterAuthorityOwnerGroupIdentity> typeGroups;
    private readonly ImmutableArray<MetadataGenericParameterAuthorityOwnerGroupIdentity> methodGroups;
    private readonly ImmutableDictionary<int, MetadataGenericParameterTableRowIdentity> parametersByToken;
    private readonly ImmutableDictionary<int, MetadataGenericParameterAuthorityOwnerGroupIdentity> parameterOwnersByToken;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataGenericParameterAuthorityCatalogIdentity(
        MetadataGenericParameterProofResultKind resultKind,
        MetadataGenericParameterProofIssue issue,
        MetadataDefinitionAuthorityCatalogIdentity definitionAuthority,
        ImmutableArray<MetadataGenericParameterAuthorityOwnerGroupIdentity> groups,
        ImmutableArray<MetadataGenericParameterAuthorityOwnerGroupIdentity> typeGroups,
        ImmutableArray<MetadataGenericParameterAuthorityOwnerGroupIdentity> methodGroups)
    {
        ResultKind = resultKind;
        Issue = issue;
        DefinitionAuthority = definitionAuthority;
        this.groups = ExpressionV2ContractEncoding.Copy(groups);
        this.typeGroups = ExpressionV2ContractEncoding.Copy(typeGroups);
        this.methodGroups = ExpressionV2ContractEncoding.Copy(methodGroups);

        var parameterBuilder = ImmutableDictionary.CreateBuilder<int, MetadataGenericParameterTableRowIdentity>();
        var parameterOwnerBuilder =
            ImmutableDictionary.CreateBuilder<int, MetadataGenericParameterAuthorityOwnerGroupIdentity>();
        foreach (var group in groups)
        {
            foreach (var parameter in group.ExactParameters)
            {
                parameterBuilder.Add(parameter.GenericParameterToken, parameter);
                parameterOwnerBuilder.Add(parameter.GenericParameterToken, group);
            }
        }
        parametersByToken = parameterBuilder.ToImmutable();
        parameterOwnersByToken = parameterOwnerBuilder.ToImmutable();
        if (resultKind == MetadataGenericParameterProofResultKind.Exact &&
            parametersByToken.Count != definitionAuthority.SourceEnds.GenericParameterRowCount)
        {
            throw new InvalidOperationException(
                "Exact GenericParam authority must retain one catalog-issued row for every physical GenericParam RID.");
        }

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32((int)resultKind);
        writer.WriteInt32((int)issue);
        writer.WriteLengthPrefixedBytes(definitionAuthority.CanonicalBytes.AsSpan());
        ExpressionV2ContractEncoding.WriteCanonicalArray(
            writer,
            groups,
            static group => group.CanonicalBytes);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets whether the complete draft owner catalog is exact, non-exact, or invalid.</summary>
    public MetadataGenericParameterProofResultKind ResultKind { get; }

    /// <summary>Gets the typed draft owner-catalog issue, or none for an exact result.</summary>
    public MetadataGenericParameterProofIssue Issue { get; }

    /// <summary>Gets the complete definition-authority prerequisite retained by this draft outcome.</summary>
    public MetadataDefinitionAuthorityCatalogIdentity DefinitionAuthority { get; }

    /// <summary>Gets the source ends retained by the definition-authority draft prerequisite.</summary>
    public MetadataSourceEndIdentity SourceEnds => DefinitionAuthority.SourceEnds;

    /// <summary>
    /// Gets a defensive TypeOrMethodDef coded-owner-order copy of exact draft groups, or an empty array otherwise.
    /// </summary>
    public ImmutableArray<MetadataGenericParameterAuthorityOwnerGroupIdentity> Groups =>
        ExpressionV2ContractEncoding.Copy(groups);

    /// <summary>Gets a prerequisite bound propagated by a non-exact authority draft, otherwise null.</summary>
    public EvaluationDeterministicBound? ReachedBound => DefinitionAuthority.ReachedBound;

    /// <summary>Gets the prerequisite's issue-related count, otherwise zero.</summary>
    public int ObservedCount => DefinitionAuthority.ObservedCount;

    /// <summary>Gets the prerequisite's issue-related metadata token, otherwise null.</summary>
    public int? RelatedMetadataToken => DefinitionAuthority.RelatedMetadataToken;

    /// <summary>Gets a defensive copy of the versioned canonical draft bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Creates the complete GenericParam owner-group draft catalog from definition authority.</summary>
    /// <param name="definitionAuthority">
    /// The complete authority catalog that alone supplies exact owners, declared arities, and physical rows.
    /// </param>
    /// <returns>An exact guarded catalog or a prefix-free prerequisite disposition.</returns>
    public static MetadataGenericParameterAuthorityCatalogIdentity Create(
        MetadataDefinitionAuthorityCatalogIdentity definitionAuthority)
    {
        ArgumentNullException.ThrowIfNull(definitionAuthority);
        if (definitionAuthority.ResultKind != MetadataDefinitionAuthorityResultKind.Exact)
        {
            return new MetadataGenericParameterAuthorityCatalogIdentity(
                definitionAuthority.ResultKind == MetadataDefinitionAuthorityResultKind.NonExact
                    ? MetadataGenericParameterProofResultKind.NonExact
                    : MetadataGenericParameterProofResultKind.Invalid,
                definitionAuthority.ResultKind == MetadataDefinitionAuthorityResultKind.NonExact
                    ? MetadataGenericParameterProofIssue.DefinitionAuthorityNonExact
                    : MetadataGenericParameterProofIssue.DefinitionAuthorityInvalid,
                definitionAuthority,
                ImmutableArray<MetadataGenericParameterAuthorityOwnerGroupIdentity>.Empty,
                ImmutableArray<MetadataGenericParameterAuthorityOwnerGroupIdentity>.Empty,
                ImmutableArray<MetadataGenericParameterAuthorityOwnerGroupIdentity>.Empty);
        }

        var authorityTypes = definitionAuthority.TypeDefinitions;
        var authorityMethods = definitionAuthority.MethodDefinitions;
        var typeBuilder =
            ImmutableArray.CreateBuilder<MetadataGenericParameterAuthorityOwnerGroupIdentity>(authorityTypes.Length);
        foreach (var typeDefinition in authorityTypes)
        {
            typeBuilder.Add(MetadataGenericParameterAuthorityOwnerGroupIdentity.CreateForTypeDefinition(
                OwnerGroupMintCapability,
                definitionAuthority,
                typeDefinition));
        }

        var methodBuilder =
            ImmutableArray.CreateBuilder<MetadataGenericParameterAuthorityOwnerGroupIdentity>(authorityMethods.Length);
        foreach (var methodDefinition in authorityMethods)
        {
            methodBuilder.Add(MetadataGenericParameterAuthorityOwnerGroupIdentity.CreateForMethodDefinition(
                OwnerGroupMintCapability,
                definitionAuthority,
                methodDefinition));
        }

        var exactTypeGroups = typeBuilder.MoveToImmutable();
        var exactMethodGroups = methodBuilder.MoveToImmutable();
        var combinedBuilder = ImmutableArray.CreateBuilder<MetadataGenericParameterAuthorityOwnerGroupIdentity>(
            exactTypeGroups.Length + exactMethodGroups.Length);
        var typeIndex = 0;
        var methodIndex = 0;
        while (typeIndex < exactTypeGroups.Length || methodIndex < exactMethodGroups.Length)
        {
            var typeKey = typeIndex < exactTypeGroups.Length
                ? TypeOrMethodDefinitionCodedIndex(exactTypeGroups[typeIndex])
                : long.MaxValue;
            var methodKey = methodIndex < exactMethodGroups.Length
                ? TypeOrMethodDefinitionCodedIndex(exactMethodGroups[methodIndex])
                : long.MaxValue;
            if (typeKey < methodKey)
            {
                combinedBuilder.Add(exactTypeGroups[typeIndex++]);
            }
            else
            {
                combinedBuilder.Add(exactMethodGroups[methodIndex++]);
            }
        }

        return new MetadataGenericParameterAuthorityCatalogIdentity(
            MetadataGenericParameterProofResultKind.Exact,
            MetadataGenericParameterProofIssue.None,
            definitionAuthority,
            combinedBuilder.MoveToImmutable(),
            exactTypeGroups,
            exactMethodGroups);
    }

    /// <summary>Finds the exact draft owner group for one authority-issued TypeDef.</summary>
    /// <param name="typeDefinition">The TypeDef authority row to locate.</param>
    /// <returns>The matching draft group, or null when this catalog did not issue that authority row.</returns>
    public MetadataGenericParameterAuthorityOwnerGroupIdentity? FindTypeDefinitionOwner(
        MetadataTypeDefinitionAuthorityIdentity typeDefinition)
    {
        ArgumentNullException.ThrowIfNull(typeDefinition);
        if (ResultKind != MetadataGenericParameterProofResultKind.Exact ||
            !typeDefinition.SourceEnds.Equals(SourceEnds))
        {
            return null;
        }

        var rowId = CanonicalReplayEncoding.MetadataTokenRowId(typeDefinition.TypeDefinitionToken);
        if (rowId <= 0 || rowId > typeGroups.Length ||
            !DefinitionAuthority.ExactTypeDefinitionOrDefault(typeDefinition.TypeDefinitionToken)!.Equals(typeDefinition))
        {
            return null;
        }
        return typeGroups[rowId - 1];
    }

    /// <summary>Finds the exact draft owner group for one authority-issued MethodDef.</summary>
    /// <param name="methodDefinition">The MethodDef authority row to locate.</param>
    /// <returns>The matching draft group, or null when this catalog did not issue that authority row.</returns>
    public MetadataGenericParameterAuthorityOwnerGroupIdentity? FindMethodDefinitionOwner(
        MetadataMethodDefinitionAuthorityIdentity methodDefinition)
    {
        ArgumentNullException.ThrowIfNull(methodDefinition);
        if (ResultKind != MetadataGenericParameterProofResultKind.Exact ||
            !methodDefinition.SourceEnds.Equals(SourceEnds))
        {
            return null;
        }

        var rowId = CanonicalReplayEncoding.MetadataTokenRowId(methodDefinition.MethodDefinitionToken);
        if (rowId <= 0 || rowId > methodGroups.Length ||
            !DefinitionAuthority.ExactMethodDefinitionOrDefault(methodDefinition.MethodDefinitionToken)!
                .Equals(methodDefinition))
        {
            return null;
        }
        return methodGroups[rowId - 1];
    }

    /// <summary>Finds one exact authority-issued GenericParam row by its physical metadata token.</summary>
    /// <param name="genericParameterToken">
    /// The non-nil GenericParam token within this catalog's exact source end.
    /// </param>
    /// <returns>
    /// The exact authority-owned draft row, or null when the retained definition-authority prerequisite is not exact.
    /// </returns>
    /// <remarks>
    /// This guarded draft lookup rejects the wrong metadata table and out-of-source-range tokens before consulting
    /// derived state. A returned row therefore cannot be supplied independently of this catalog's exact authority.
    /// </remarks>
    public MetadataGenericParameterTableRowIdentity? FindGenericParameter(int genericParameterToken)
    {
        if (!CanonicalReplayEncoding.IsMetadataTokenForTable(genericParameterToken, 0x2A))
        {
            throw new ArgumentOutOfRangeException(nameof(genericParameterToken));
        }

        var rowId = CanonicalReplayEncoding.MetadataTokenRowId(genericParameterToken);
        if (rowId <= 0 || rowId > SourceEnds.GenericParameterRowCount)
        {
            throw new ArgumentOutOfRangeException(nameof(genericParameterToken));
        }

        return ResultKind == MetadataGenericParameterProofResultKind.Exact &&
               parametersByToken.TryGetValue(genericParameterToken, out var parameter)
            ? parameter
            : null;
    }

    /// <summary>Tests canonical equality between two authority-owned GenericParam draft catalogs.</summary>
    /// <param name="other">The other complete draft catalog.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(MetadataGenericParameterAuthorityCatalogIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests authority-owned GenericParam draft catalog equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a catalog with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataGenericParameterAuthorityCatalogIdentity);

    /// <summary>Computes a deterministic hash code from immutable canonical draft content.</summary>
    /// <returns>A hash code for this complete authority-owned GenericParam draft catalog.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static bool OwnsOwnerGroupMintCapability(object? capability) =>
        ReferenceEquals(capability, OwnerGroupMintCapability);

    internal MetadataGenericParameterAuthorityOwnerGroupIdentity? ExactOwnerGroupOrDefault(
        int genericParameterToken)
    {
        if (ResultKind != MetadataGenericParameterProofResultKind.Exact ||
            !CanonicalReplayEncoding.IsMetadataTokenForTable(genericParameterToken, 0x2A))
        {
            return null;
        }

        var rowId = CanonicalReplayEncoding.MetadataTokenRowId(genericParameterToken);
        return rowId > 0 && rowId <= SourceEnds.GenericParameterRowCount &&
               parameterOwnersByToken.TryGetValue(genericParameterToken, out var owner)
            ? owner
            : null;
    }

    private static long TypeOrMethodDefinitionCodedIndex(
        MetadataGenericParameterAuthorityOwnerGroupIdentity group)
    {
        var rowId = CanonicalReplayEncoding.MetadataTokenRowId(group.OwnerMetadataToken);
        var tag = group.OwnerKind == MetadataGenericParameterOwnerKind.MethodDefinition ? 1 : 0;
        return ((long)(uint)rowId << 1) | (uint)tag;
    }
}

/// <summary>Classifies one authority-row-addressed GenericParam binding observation.</summary>
/// <remarks>
/// The draft discriminator distinguishes an exact closed argument from an explicitly unavailable observation.
/// </remarks>
public enum MetadataGenericParameterAuthorityBindingKind
{
    /// <summary>The exact authority-issued GenericParam row maps to one exact closed argument.</summary>
    Exact = 1,

    /// <summary>The exact authority-issued GenericParam row has no observed closed argument.</summary>
    Unavailable = 2,
}

/// <summary>Freezes one authority-row-addressed exact or unavailable GenericParam binding.</summary>
/// <remarks>
/// This sealed draft identity names the physical table-row identity directly. Exact bindings remain internally issued
/// from source-derived closed types; public callers may only record explicit unavailability. The Product.DumpQuery
/// assembly is the provisional draft exact-observation issuer boundary until the complete producer is introduced.
/// </remarks>
public sealed class MetadataGenericParameterAuthorityBindingIdentity :
    IEquatable<MetadataGenericParameterAuthorityBindingIdentity>
{
    private const string CanonicalDomain = "metadata-v2-generic-parameter-authority-binding";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataGenericParameterAuthorityBindingIdentity(
        MetadataGenericParameterAuthorityBindingKind kind,
        MetadataGenericParameterTableRowIdentity parameter,
        MetadataClosedTypeIdentity? argument)
    {
        Kind = kind;
        Parameter = parameter;
        Argument = argument;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32((int)kind);
        writer.WriteLengthPrefixedBytes(parameter.CanonicalBytes.AsSpan());
        writer.WriteBoolean(argument is not null);
        if (argument is not null)
        {
            writer.WriteLengthPrefixedBytes(argument.CanonicalBytes.AsSpan());
        }
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact draft binding observation kind.</summary>
    public MetadataGenericParameterAuthorityBindingKind Kind { get; }

    /// <summary>Gets the exact authority-issued GenericParam table row.</summary>
    public MetadataGenericParameterTableRowIdentity Parameter { get; }

    /// <summary>Gets the exact closed argument only for an exact draft binding.</summary>
    public MetadataClosedTypeIdentity? Argument { get; }

    /// <summary>Gets a defensive copy of the versioned canonical draft bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one authority-issued GenericParam row with no observed closed binding.</summary>
    /// <param name="parameter">The exact authority-issued physical table row.</param>
    /// <returns>A sealed immutable unavailable draft binding.</returns>
    public static MetadataGenericParameterAuthorityBindingIdentity Unavailable(
        MetadataGenericParameterTableRowIdentity parameter)
    {
        ArgumentNullException.ThrowIfNull(parameter);
        return new MetadataGenericParameterAuthorityBindingIdentity(
            MetadataGenericParameterAuthorityBindingKind.Unavailable,
            parameter,
            null);
    }

    /// <summary>Tests canonical equality between two authority-row-addressed draft bindings.</summary>
    /// <param name="other">The other draft binding.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(MetadataGenericParameterAuthorityBindingIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests authority-row-addressed draft binding equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for an identity with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataGenericParameterAuthorityBindingIdentity);

    /// <summary>Computes a deterministic hash code from immutable canonical draft content.</summary>
    /// <returns>A hash code for this authority-row-addressed draft binding.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static MetadataGenericParameterAuthorityBindingIdentity Exact(
        MetadataGenericParameterTableRowIdentity parameter,
        MetadataClosedTypeIdentity argument)
    {
        ArgumentNullException.ThrowIfNull(parameter);
        ArgumentNullException.ThrowIfNull(argument);
        return new MetadataGenericParameterAuthorityBindingIdentity(
            MetadataGenericParameterAuthorityBindingKind.Exact,
            parameter,
            argument);
    }
}

/// <summary>Freezes one complete authority-row-addressed binding ledger for an authority-issued owner group.</summary>
/// <remarks>
/// This sealed draft ledger normalizes caller observations by authority-proven Number. Missing evidence and evaluator
/// arity stops expose no binding prefix; contradictory rows remain typed invalid results.
/// </remarks>
public sealed class MetadataGenericParameterAuthorityBindingLedgerIdentity :
    IEquatable<MetadataGenericParameterAuthorityBindingLedgerIdentity>
{
    private const string CanonicalDomain = "metadata-v2-generic-parameter-authority-binding-ledger";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<MetadataGenericParameterAuthorityBindingIdentity> bindings;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataGenericParameterAuthorityBindingLedgerIdentity(
        MetadataGenericParameterProofResultKind resultKind,
        MetadataGenericParameterProofIssue issue,
        MetadataGenericParameterAuthorityOwnerGroupIdentity ownerGroup,
        ImmutableArray<MetadataGenericParameterAuthorityBindingIdentity> bindings,
        EvaluationDeterministicBound? reachedBound,
        int observedCount)
    {
        ResultKind = resultKind;
        Issue = issue;
        OwnerGroup = ownerGroup;
        this.bindings = ExpressionV2ContractEncoding.Copy(bindings);
        ReachedBound = reachedBound;
        ObservedCount = observedCount;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32((int)resultKind);
        writer.WriteInt32((int)issue);
        writer.WriteLengthPrefixedBytes(ownerGroup.CanonicalBytes.AsSpan());
        ExpressionV2ContractEncoding.WriteCanonicalArray(
            writer,
            bindings,
            static binding => binding.CanonicalBytes);
        ExpressionV2ContractEncoding.WriteOptionalBound(writer, reachedBound);
        writer.WriteInt32(observedCount);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets whether the complete draft binding ledger is exact, non-exact, or invalid.</summary>
    public MetadataGenericParameterProofResultKind ResultKind { get; }

    /// <summary>Gets the typed draft ledger issue, or none for an exact result.</summary>
    public MetadataGenericParameterProofIssue Issue { get; }

    /// <summary>Gets the authority-issued exact owner group retained by this draft ledger.</summary>
    public MetadataGenericParameterAuthorityOwnerGroupIdentity OwnerGroup { get; }

    /// <summary>Gets the definition authority that issued the ledger's draft owner group.</summary>
    public MetadataDefinitionAuthorityCatalogIdentity DefinitionAuthority => OwnerGroup.DefinitionAuthority;

    /// <summary>Gets a defensive Number-ordered copy of exact draft bindings, or an empty array otherwise.</summary>
    public ImmutableArray<MetadataGenericParameterAuthorityBindingIdentity> Bindings =>
        ExpressionV2ContractEncoding.Copy(bindings);

    /// <summary>Gets the evaluator arity limit only for a prefix-free arity stop.</summary>
    public EvaluationDeterministicBound? ReachedBound { get; }

    /// <summary>Gets limit plus one for an arity stop, otherwise zero.</summary>
    public int ObservedCount { get; }

    /// <summary>Gets a defensive copy of the versioned canonical draft bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one complete authority-row-addressed draft binding ledger.</summary>
    /// <param name="ownerGroup">The exact authority-issued TypeDef or MethodDef owner group.</param>
    /// <param name="bindings">One row-addressed observation per declared parameter, in any caller order.</param>
    /// <returns>An exact normalized ledger, prefix-free stop, or typed invalid draft result.</returns>
    public static MetadataGenericParameterAuthorityBindingLedgerIdentity Create(
        MetadataGenericParameterAuthorityOwnerGroupIdentity ownerGroup,
        ImmutableArray<MetadataGenericParameterAuthorityBindingIdentity> bindings)
    {
        ArgumentNullException.ThrowIfNull(ownerGroup);
        if (!ownerGroup.IsAdmitted)
        {
            return new MetadataGenericParameterAuthorityBindingLedgerIdentity(
                MetadataGenericParameterProofResultKind.NonExact,
                MetadataGenericParameterProofIssue.OwnerArityBoundReached,
                ownerGroup,
                ImmutableArray<MetadataGenericParameterAuthorityBindingIdentity>.Empty,
                ownerGroup.ReachedBound,
                ownerGroup.ObservedCount);
        }

        var parameters = ownerGroup.ExactParameters;
        if (bindings.IsDefault || bindings.Length < parameters.Length)
        {
            return new MetadataGenericParameterAuthorityBindingLedgerIdentity(
                MetadataGenericParameterProofResultKind.NonExact,
                MetadataGenericParameterProofIssue.BindingIncomplete,
                ownerGroup,
                ImmutableArray<MetadataGenericParameterAuthorityBindingIdentity>.Empty,
                null,
                0);
        }
        if (bindings.Length > parameters.Length)
        {
            return Invalid(ownerGroup, MetadataGenericParameterProofIssue.BindingRowMismatch);
        }

        var selectedByToken = parameters.ToDictionary(
            static parameter => parameter.GenericParameterToken,
            static parameter => parameter);
        var bindingsByToken = new Dictionary<int, MetadataGenericParameterAuthorityBindingIdentity>();
        foreach (var binding in bindings)
        {
            if (binding is null)
            {
                return Invalid(ownerGroup, MetadataGenericParameterProofIssue.BindingRowMismatch);
            }
            if (!binding.Parameter.SourceEnds.Equals(ownerGroup.SourceEnds))
            {
                return Invalid(ownerGroup, MetadataGenericParameterProofIssue.SourceModuleMismatch);
            }
            if (!selectedByToken.TryGetValue(binding.Parameter.GenericParameterToken, out var selected) ||
                !selected.Equals(binding.Parameter))
            {
                return Invalid(ownerGroup, MetadataGenericParameterProofIssue.BindingRowMismatch);
            }
            if (!bindingsByToken.TryAdd(binding.Parameter.GenericParameterToken, binding))
            {
                return Invalid(ownerGroup, MetadataGenericParameterProofIssue.DuplicateBinding);
            }
        }
        if (bindingsByToken.Count != parameters.Length)
        {
            return new MetadataGenericParameterAuthorityBindingLedgerIdentity(
                MetadataGenericParameterProofResultKind.NonExact,
                MetadataGenericParameterProofIssue.BindingIncomplete,
                ownerGroup,
                ImmutableArray<MetadataGenericParameterAuthorityBindingIdentity>.Empty,
                null,
                0);
        }

        var normalized = parameters
            .Select(parameter => bindingsByToken[parameter.GenericParameterToken])
            .ToImmutableArray();
        return new MetadataGenericParameterAuthorityBindingLedgerIdentity(
            MetadataGenericParameterProofResultKind.Exact,
            MetadataGenericParameterProofIssue.None,
            ownerGroup,
            normalized,
            null,
            0);
    }

    /// <summary>Tests canonical equality between two authority-row-addressed draft binding ledgers.</summary>
    /// <param name="other">The other complete draft ledger.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(MetadataGenericParameterAuthorityBindingLedgerIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests authority-row-addressed draft ledger equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a ledger with identical canonical draft content.</returns>
    public override bool Equals(object? obj) =>
        Equals(obj as MetadataGenericParameterAuthorityBindingLedgerIdentity);

    /// <summary>Computes a deterministic hash code from immutable canonical draft content.</summary>
    /// <returns>A hash code for this complete authority-row-addressed draft binding ledger.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal MetadataGenericParameterAuthorityBindingIdentity? FindBinding(int number) =>
        ResultKind == MetadataGenericParameterProofResultKind.Exact &&
        number >= 0 && number < bindings.Length && bindings[number].Parameter.Number == number
            ? bindings[number]
            : null;

    private static MetadataGenericParameterAuthorityBindingLedgerIdentity Invalid(
        MetadataGenericParameterAuthorityOwnerGroupIdentity ownerGroup,
        MetadataGenericParameterProofIssue issue) =>
        new(
            MetadataGenericParameterProofResultKind.Invalid,
            issue,
            ownerGroup,
            ImmutableArray<MetadataGenericParameterAuthorityBindingIdentity>.Empty,
            null,
            0);
}
