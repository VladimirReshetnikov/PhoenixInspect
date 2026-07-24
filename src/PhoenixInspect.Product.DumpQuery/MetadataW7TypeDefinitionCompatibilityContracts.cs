using System.Collections.Immutable;
using PhoenixInspect.Core.Abstractions;

namespace PhoenixInspect.Product.DumpQuery;

/// <summary>Classifies one authority-to-W7 TypeDef comparison.</summary>
/// <remarks>
/// A compatible result means the candidate agrees with the authority-issued physical TypeDef chain and with the
/// direct interpretation of the authority's resolved FieldDef and MethodDef ownership. The candidate remains a
/// comparison input and never becomes an authority issuer.
/// </remarks>
public enum MetadataW7TypeDefinitionCompatibilityResultKind
{
    /// <summary>The W7 candidate agrees with every field compared by this certificate.</summary>
    Compatible = 1,

    /// <summary>No W7 candidate was supplied for this authority-issued TypeDef row.</summary>
    CandidateAbsent = 2,

    /// <summary>The supplied W7 candidate disagrees with at least one compared authority fact.</summary>
    Mismatch = 3,
}

/// <summary>Identifies the first deterministic issue in one authority-to-W7 TypeDef comparison.</summary>
/// <remarks>
/// The issue order is stable for explanation and replay. Enclosing-type mismatch covers the complete retained W7
/// parent chain; member-ownership issues compare resolved authority tokens with the candidate interval's direct-table
/// interpretation.
/// </remarks>
public enum MetadataW7TypeDefinitionCompatibilityIssue
{
    /// <summary>No issue applies to a compatible certificate.</summary>
    None = 0,

    /// <summary>No W7 candidate was supplied for the authority TypeDef.</summary>
    CandidateAbsent = 1,

    /// <summary>The candidate names a different exact metadata module.</summary>
    SourceMismatch = 2,

    /// <summary>The candidate names a different TypeDef token.</summary>
    TypeDefinitionTokenMismatch = 3,

    /// <summary>The candidate carries a different decoded namespace.</summary>
    NamespaceNameMismatch = 4,

    /// <summary>The candidate carries a different uninterpreted metadata name.</summary>
    TypeNameMismatch = 5,

    /// <summary>The candidate carries different raw TypeAttributes bits.</summary>
    TypeAttributesMismatch = 6,

    /// <summary>The candidate carries a different decoded Extends token.</summary>
    ExtendsMetadataTokenMismatch = 7,

    /// <summary>The candidate carries a different raw FieldList start.</summary>
    FieldListStartMismatch = 8,

    /// <summary>The candidate carries a different complete-table-derived FieldList end.</summary>
    FieldListEndMismatch = 9,

    /// <summary>The candidate carries a different raw MethodList start.</summary>
    MethodListStartMismatch = 10,

    /// <summary>The candidate carries a different complete-table-derived MethodList end.</summary>
    MethodListEndMismatch = 11,

    /// <summary>The candidate carries a different total physical GenericParam count.</summary>
    GenericParameterCountMismatch = 12,

    /// <summary>The candidate's complete enclosing TypeDef chain disagrees with the authority parent chain.</summary>
    EnclosingTypeMismatch = 13,

    /// <summary>The candidate interval's direct FieldDef sequence differs from resolved authority ownership.</summary>
    FieldOwnershipMismatch = 14,

    /// <summary>The candidate interval's direct MethodDef sequence differs from resolved authority ownership.</summary>
    MethodOwnershipMismatch = 15,
}

/// <summary>Identifies the active physical member-list domain used by one compatibility certificate.</summary>
/// <remarks>
/// The value explains whether authority ownership was read directly from the definition table or resolved through a
/// complete pointer table. It does not let a W7 interval assert pointer-table contents.
/// </remarks>
public enum MetadataW7MemberOwnershipDomainKind
{
    /// <summary>The active list positions are definition-table row identifiers.</summary>
    DirectDefinitionTable = 1,

    /// <summary>The active list positions are pointer-table row identifiers resolved by the authority.</summary>
    PointerTable = 2,
}

/// <summary>Classifies one complete authority-to-W7 TypeDef catalog.</summary>
/// <remarks>
/// Exact means every authority TypeDef RID received one complete comparison outcome. It does not require every
/// candidate to be present or compatible. Non-exact and invalid results expose no certificate prefix.
/// </remarks>
public enum MetadataW7TypeDefinitionCompatibilityCatalogResultKind
{
    /// <summary>Every authority TypeDef received one complete RID-ordered comparison.</summary>
    Exact = 1,

    /// <summary>A prerequisite or candidate vector stopped before a complete comparison.</summary>
    NonExact = 2,

    /// <summary>A prerequisite or candidate vector contains contradictory complete input.</summary>
    Invalid = 3,
}

/// <summary>Identifies the typed issue for one authority-to-W7 TypeDef catalog.</summary>
/// <remarks>
/// Candidate absence in an initialized complete vector is a row outcome rather than a catalog issue. These values
/// explain only why a complete RID-ordered comparison catalog could not be emitted.
/// </remarks>
public enum MetadataW7TypeDefinitionCompatibilityCatalogIssue
{
    /// <summary>No catalog-level issue applies to an exact complete comparison.</summary>
    None = 0,

    /// <summary>The definition-authority prerequisite was non-exact.</summary>
    DefinitionAuthorityNonExact = 1,

    /// <summary>The definition-authority prerequisite was invalid.</summary>
    DefinitionAuthorityInvalid = 2,

    /// <summary>The candidate vector was default or ended before the last authority TypeDef RID.</summary>
    CandidateSlotsIncomplete = 3,

    /// <summary>The candidate vector contained more slots than the exact authority has TypeDef rows.</summary>
    CandidateSlotCountConflict = 4,
}

/// <summary>Freezes one guarded authority-to-W7 TypeDef compatibility outcome.</summary>
/// <remarks>
/// This sealed certificate retains the authority-issued TypeDef and the optional W7 comparison candidate. Resolved
/// member tokens always come from the authority, including pointer layouts. The contract compares the physical fields
/// formerly used by the W7 bridge but deliberately does not treat W7 introduced arity, FieldDef row content, or
/// MethodDef parameter ownership as independently proven facts. Only the complete catalog can mint certificates.
/// </remarks>
public sealed class MetadataW7TypeDefinitionCompatibilityCertificateIdentity :
    IEquatable<MetadataW7TypeDefinitionCompatibilityCertificateIdentity>
{
    private const string CanonicalDomain = "metadata-v2-w7-typedef-compatibility-certificate";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataW7TypeDefinitionCompatibilityCertificateIdentity(
        MetadataW7TypeDefinitionCompatibilityResultKind resultKind,
        MetadataW7TypeDefinitionCompatibilityIssue issue,
        MetadataTypeDefinitionAuthorityIdentity authorityTypeDefinition,
        StaticFieldTypeDefinitionIdentity? candidate,
        MetadataW7MemberOwnershipDomainKind fieldOwnershipDomain,
        MetadataW7MemberOwnershipDomainKind methodOwnershipDomain)
    {
        ResultKind = resultKind;
        Issue = issue;
        AuthorityTypeDefinition = authorityTypeDefinition;
        Candidate = candidate;
        FieldOwnershipDomain = fieldOwnershipDomain;
        MethodOwnershipDomain = methodOwnershipDomain;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32((int)resultKind);
        writer.WriteInt32((int)issue);
        writer.WriteInt32(authorityTypeDefinition.TypeDefinitionToken);
        writer.WriteSha256(authorityTypeDefinition.Sha256, nameof(authorityTypeDefinition));
        writer.WriteBoolean(candidate is not null);
        if (candidate is not null)
        {
            writer.WriteInt32(candidate.TypeDefinitionToken);
            writer.WriteSha256(candidate.Sha256, nameof(candidate));
        }
        writer.WriteInt32((int)fieldOwnershipDomain);
        writer.WriteInt32((int)methodOwnershipDomain);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets whether this complete comparison is compatible, absent, or mismatched.</summary>
    public MetadataW7TypeDefinitionCompatibilityResultKind ResultKind { get; }

    /// <summary>Gets the first deterministic issue, or none for a compatible result.</summary>
    public MetadataW7TypeDefinitionCompatibilityIssue Issue { get; }

    /// <summary>Gets the exact authority-issued TypeDef row that remains the source of physical facts.</summary>
    public MetadataTypeDefinitionAuthorityIdentity AuthorityTypeDefinition { get; }

    /// <summary>Gets the retained W7 comparison candidate, or null for an explicit absent-candidate outcome.</summary>
    public StaticFieldTypeDefinitionIdentity? Candidate { get; }

    /// <summary>Gets the active domain from which the authority resolved FieldDef ownership.</summary>
    public MetadataW7MemberOwnershipDomainKind FieldOwnershipDomain { get; }

    /// <summary>Gets the active domain from which the authority resolved MethodDef ownership.</summary>
    public MetadataW7MemberOwnershipDomainKind MethodOwnershipDomain { get; }

    /// <summary>Gets a defensive authority-issued ownership-order copy of resolved FieldDef tokens.</summary>
    public ImmutableArray<int> FieldDefinitionTokens => AuthorityTypeDefinition.FieldDefinitionTokens;

    /// <summary>Gets a defensive authority-issued ownership-order copy of resolved MethodDef tokens.</summary>
    public ImmutableArray<int> MethodDefinitionTokens => AuthorityTypeDefinition.MethodDefinitionTokens;

    /// <summary>Gets whether every field compared by this certificate agrees.</summary>
    public bool IsCompatible => ResultKind == MetadataW7TypeDefinitionCompatibilityResultKind.Compatible;

    /// <summary>Gets a defensive copy of the bounded versioned canonical certificate bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical certificate bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Tests canonical equality between two authority-to-W7 TypeDef certificates.</summary>
    /// <param name="other">The other certificate.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(MetadataW7TypeDefinitionCompatibilityCertificateIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests authority-to-W7 TypeDef certificate equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a certificate with identical canonical content.</returns>
    public override bool Equals(object? obj) =>
        Equals(obj as MetadataW7TypeDefinitionCompatibilityCertificateIdentity);

    /// <summary>Computes a deterministic hash code from immutable canonical certificate content.</summary>
    /// <returns>A hash code for this authority-to-W7 TypeDef certificate.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static MetadataW7TypeDefinitionCompatibilityCertificateIdentity Create(
        object mintCapability,
        MetadataTypeDefinitionAuthorityIdentity authorityTypeDefinition,
        StaticFieldTypeDefinitionIdentity? candidate,
        MetadataW7TypeDefinitionCompatibilityIssue issue)
    {
        if (!MetadataW7TypeDefinitionCompatibilityCatalogIdentity.OwnsCertificateMintCapability(mintCapability))
        {
            throw new ArgumentException(
                "A W7 TypeDef compatibility certificate requires the complete catalog's private mint capability.",
                nameof(mintCapability));
        }

        ArgumentNullException.ThrowIfNull(authorityTypeDefinition);
        ExpressionV2ContractEncoding.RequireDefined(issue, nameof(issue));
        if (candidate is null != (issue == MetadataW7TypeDefinitionCompatibilityIssue.CandidateAbsent))
        {
            throw new ArgumentException(
                "Candidate absence and the compatibility issue must agree.",
                nameof(candidate));
        }

        var resultKind = issue switch
        {
            MetadataW7TypeDefinitionCompatibilityIssue.None =>
                MetadataW7TypeDefinitionCompatibilityResultKind.Compatible,
            MetadataW7TypeDefinitionCompatibilityIssue.CandidateAbsent =>
                MetadataW7TypeDefinitionCompatibilityResultKind.CandidateAbsent,
            _ => MetadataW7TypeDefinitionCompatibilityResultKind.Mismatch,
        };
        return new MetadataW7TypeDefinitionCompatibilityCertificateIdentity(
            resultKind,
            issue,
            authorityTypeDefinition,
            candidate,
            authorityTypeDefinition.SourceEnds.FieldPointerRowCount == 0
                ? MetadataW7MemberOwnershipDomainKind.DirectDefinitionTable
                : MetadataW7MemberOwnershipDomainKind.PointerTable,
            authorityTypeDefinition.SourceEnds.MethodPointerRowCount == 0
                ? MetadataW7MemberOwnershipDomainKind.DirectDefinitionTable
                : MetadataW7MemberOwnershipDomainKind.PointerTable);
    }
}

/// <summary>Freezes the complete guarded authority-to-W7 TypeDef comparison catalog.</summary>
/// <remarks>
/// This sealed catalog embeds the definition authority once, compares one nullable candidate slot per TypeDef RID,
/// and is the sole issuer of row certificates. Exact catalogs include absent and mismatched row outcomes; every
/// prerequisite or vector-shape stop is prefix-free. W7 objects remain comparison candidates only.
/// </remarks>
public sealed class MetadataW7TypeDefinitionCompatibilityCatalogIdentity :
    IEquatable<MetadataW7TypeDefinitionCompatibilityCatalogIdentity>
{
    private const string CanonicalDomain = "metadata-v2-w7-typedef-compatibility-catalog";
    private const int CanonicalSchemaVersion = 1;
    private static readonly object CertificateMintCapability = new();
    private readonly ImmutableArray<MetadataW7TypeDefinitionCompatibilityCertificateIdentity> certificates;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataW7TypeDefinitionCompatibilityCatalogIdentity(
        MetadataW7TypeDefinitionCompatibilityCatalogResultKind resultKind,
        MetadataW7TypeDefinitionCompatibilityCatalogIssue issue,
        MetadataDefinitionAuthorityCatalogIdentity definitionAuthority,
        ImmutableArray<MetadataW7TypeDefinitionCompatibilityCertificateIdentity> certificates,
        EvaluationDeterministicBound? reachedBound,
        int observedCount,
        int? relatedMetadataToken)
    {
        ResultKind = resultKind;
        Issue = issue;
        DefinitionAuthority = definitionAuthority;
        this.certificates = ExpressionV2ContractEncoding.Copy(certificates);
        ReachedBound = reachedBound;
        ObservedCount = observedCount;
        RelatedMetadataToken = relatedMetadataToken;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32((int)resultKind);
        writer.WriteInt32((int)issue);
        writer.WriteLengthPrefixedBytes(definitionAuthority.CanonicalBytes.AsSpan());
        ExpressionV2ContractEncoding.WriteCanonicalArray(
            writer,
            certificates,
            static certificate => certificate.CanonicalBytes);
        ExpressionV2ContractEncoding.WriteOptionalBound(writer, reachedBound);
        writer.WriteInt32(observedCount);
        ExpressionV2ContractEncoding.WriteOptionalInt32(writer, relatedMetadataToken);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets whether this complete comparison catalog is exact, non-exact, or invalid.</summary>
    public MetadataW7TypeDefinitionCompatibilityCatalogResultKind ResultKind { get; }

    /// <summary>Gets the typed catalog-level issue, or none for an exact complete comparison.</summary>
    public MetadataW7TypeDefinitionCompatibilityCatalogIssue Issue { get; }

    /// <summary>Gets the complete definition-authority prerequisite retained by this catalog.</summary>
    public MetadataDefinitionAuthorityCatalogIdentity DefinitionAuthority { get; }

    /// <summary>Gets the detailed issue retained by the definition-authority prerequisite.</summary>
    public MetadataDefinitionAuthorityIssue DefinitionAuthorityIssue => DefinitionAuthority.Issue;

    /// <summary>Gets a prerequisite operation bound propagated by a non-exact authority, otherwise null.</summary>
    public EvaluationDeterministicBound? ReachedBound { get; }

    /// <summary>Gets the issue-related prerequisite or candidate-slot observation count.</summary>
    public int ObservedCount { get; }

    /// <summary>Gets the issue-related metadata token, otherwise null.</summary>
    public int? RelatedMetadataToken { get; }

    /// <summary>
    /// Gets a defensive TypeDef-RID-order copy of all certificates, or an empty array for a catalog stop.
    /// </summary>
    public ImmutableArray<MetadataW7TypeDefinitionCompatibilityCertificateIdentity> Certificates =>
        ExpressionV2ContractEncoding.Copy(certificates);

    /// <summary>Gets a defensive copy of the versioned canonical catalog bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical catalog bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one complete authority-to-W7 TypeDef comparison catalog.</summary>
    /// <param name="definitionAuthority">The complete physical definition-authority prerequisite.</param>
    /// <param name="candidatesByTypeDefinitionRid">
    /// Exactly one nullable W7 candidate slot per authority TypeDef RID; null explicitly records candidate absence.
    /// </param>
    /// <returns>
    /// An exact complete comparison catalog, a prefix-free non-exact prerequisite/vector stop, or a factless invalid
    /// vector-shape result.
    /// </returns>
    public static MetadataW7TypeDefinitionCompatibilityCatalogIdentity Create(
        MetadataDefinitionAuthorityCatalogIdentity definitionAuthority,
        ImmutableArray<StaticFieldTypeDefinitionIdentity?> candidatesByTypeDefinitionRid)
    {
        ArgumentNullException.ThrowIfNull(definitionAuthority);
        if (definitionAuthority.ResultKind == MetadataDefinitionAuthorityResultKind.NonExact)
        {
            return Stopped(
                MetadataW7TypeDefinitionCompatibilityCatalogResultKind.NonExact,
                MetadataW7TypeDefinitionCompatibilityCatalogIssue.DefinitionAuthorityNonExact,
                definitionAuthority,
                definitionAuthority.ReachedBound,
                definitionAuthority.ObservedCount,
                definitionAuthority.RelatedMetadataToken);
        }
        if (definitionAuthority.ResultKind == MetadataDefinitionAuthorityResultKind.Invalid)
        {
            return Stopped(
                MetadataW7TypeDefinitionCompatibilityCatalogResultKind.Invalid,
                MetadataW7TypeDefinitionCompatibilityCatalogIssue.DefinitionAuthorityInvalid,
                definitionAuthority,
                null,
                definitionAuthority.ObservedCount,
                definitionAuthority.RelatedMetadataToken);
        }

        var typeDefinitions = definitionAuthority.TypeDefinitions;
        if (candidatesByTypeDefinitionRid.IsDefault ||
            candidatesByTypeDefinitionRid.Length < typeDefinitions.Length)
        {
            var observedCount = candidatesByTypeDefinitionRid.IsDefault
                ? 0
                : candidatesByTypeDefinitionRid.Length;
            return Stopped(
                MetadataW7TypeDefinitionCompatibilityCatalogResultKind.NonExact,
                MetadataW7TypeDefinitionCompatibilityCatalogIssue.CandidateSlotsIncomplete,
                definitionAuthority,
                null,
                observedCount,
                typeDefinitions[observedCount].TypeDefinitionToken);
        }
        if (candidatesByTypeDefinitionRid.Length > typeDefinitions.Length)
        {
            return Stopped(
                MetadataW7TypeDefinitionCompatibilityCatalogResultKind.Invalid,
                MetadataW7TypeDefinitionCompatibilityCatalogIssue.CandidateSlotCountConflict,
                definitionAuthority,
                null,
                candidatesByTypeDefinitionRid.Length,
                null);
        }

        var candidateCopy = ExpressionV2ContractEncoding.Copy(candidatesByTypeDefinitionRid);
        var builder =
            ImmutableArray.CreateBuilder<MetadataW7TypeDefinitionCompatibilityCertificateIdentity>(
                typeDefinitions.Length);
        foreach (var authorityTypeDefinition in typeDefinitions)
        {
            var candidate = candidateCopy[CanonicalReplayEncoding.MetadataTokenRowId(
                authorityTypeDefinition.TypeDefinitionToken) - 1];
            var issue = CompareCandidate(
                definitionAuthority,
                authorityTypeDefinition,
                candidate,
                candidateCopy);
            builder.Add(MetadataW7TypeDefinitionCompatibilityCertificateIdentity.Create(
                CertificateMintCapability,
                authorityTypeDefinition,
                candidate,
                issue));
        }

        return new MetadataW7TypeDefinitionCompatibilityCatalogIdentity(
            MetadataW7TypeDefinitionCompatibilityCatalogResultKind.Exact,
            MetadataW7TypeDefinitionCompatibilityCatalogIssue.None,
            definitionAuthority,
            builder.MoveToImmutable(),
            null,
            0,
            null);
    }

    /// <summary>Tests canonical equality between two complete authority-to-W7 TypeDef catalogs.</summary>
    /// <param name="other">The other catalog.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(MetadataW7TypeDefinitionCompatibilityCatalogIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests authority-to-W7 TypeDef catalog equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a catalog with identical canonical content.</returns>
    public override bool Equals(object? obj) =>
        Equals(obj as MetadataW7TypeDefinitionCompatibilityCatalogIdentity);

    /// <summary>Computes a deterministic hash code from immutable canonical catalog content.</summary>
    /// <returns>A hash code for this complete authority-to-W7 TypeDef catalog.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal MetadataW7TypeDefinitionCompatibilityCertificateIdentity? CompleteCertificateOrDefault(
        int typeDefinitionToken)
    {
        if (ResultKind != MetadataW7TypeDefinitionCompatibilityCatalogResultKind.Exact ||
            !CanonicalReplayEncoding.IsMetadataTokenForTable(typeDefinitionToken, 0x02))
        {
            return null;
        }

        var rowId = CanonicalReplayEncoding.MetadataTokenRowId(typeDefinitionToken);
        return rowId > 0 && rowId <= certificates.Length
            ? certificates[rowId - 1]
            : null;
    }

    internal static bool OwnsCertificateMintCapability(object? capability) =>
        ReferenceEquals(capability, CertificateMintCapability);

    private static MetadataW7TypeDefinitionCompatibilityIssue CompareCandidate(
        MetadataDefinitionAuthorityCatalogIdentity definitionAuthority,
        MetadataTypeDefinitionAuthorityIdentity authorityTypeDefinition,
        StaticFieldTypeDefinitionIdentity? candidate,
        ImmutableArray<StaticFieldTypeDefinitionIdentity?> candidates)
    {
        if (candidate is null)
        {
            return MetadataW7TypeDefinitionCompatibilityIssue.CandidateAbsent;
        }

        var issue = CompareScalarFields(authorityTypeDefinition, candidate);
        if (issue != MetadataW7TypeDefinitionCompatibilityIssue.None)
        {
            return issue;
        }
        if (!EnclosingChainMatches(definitionAuthority, authorityTypeDefinition, candidate, candidates))
        {
            return MetadataW7TypeDefinitionCompatibilityIssue.EnclosingTypeMismatch;
        }
        if (!DirectIntervalMatches(
                authorityTypeDefinition.FieldDefinitionTokens,
                candidate.FieldListRowId,
                candidate.FieldListEndExclusiveRowId,
                tableId: 0x04))
        {
            return MetadataW7TypeDefinitionCompatibilityIssue.FieldOwnershipMismatch;
        }
        if (!DirectIntervalMatches(
                authorityTypeDefinition.MethodDefinitionTokens,
                candidate.MethodListRowId,
                candidate.MethodListEndExclusiveRowId,
                tableId: 0x06))
        {
            return MetadataW7TypeDefinitionCompatibilityIssue.MethodOwnershipMismatch;
        }
        return MetadataW7TypeDefinitionCompatibilityIssue.None;
    }

    private static MetadataW7TypeDefinitionCompatibilityIssue CompareScalarFields(
        MetadataTypeDefinitionAuthorityIdentity authorityTypeDefinition,
        StaticFieldTypeDefinitionIdentity candidate)
    {
        var row = authorityTypeDefinition.TableRow;
        var observation = row.Observation;
        if (!authorityTypeDefinition.SourceEnds.SourceModule.Equals(candidate.MetadataModule))
        {
            return MetadataW7TypeDefinitionCompatibilityIssue.SourceMismatch;
        }
        if (authorityTypeDefinition.TypeDefinitionToken != candidate.TypeDefinitionToken)
        {
            return MetadataW7TypeDefinitionCompatibilityIssue.TypeDefinitionTokenMismatch;
        }
        if (!StringComparer.Ordinal.Equals(authorityTypeDefinition.NamespaceName, candidate.NamespaceName))
        {
            return MetadataW7TypeDefinitionCompatibilityIssue.NamespaceNameMismatch;
        }
        if (!StringComparer.Ordinal.Equals(authorityTypeDefinition.TypeName, candidate.TypeName))
        {
            return MetadataW7TypeDefinitionCompatibilityIssue.TypeNameMismatch;
        }
        if (observation.TypeAttributes != candidate.TypeAttributes)
        {
            return MetadataW7TypeDefinitionCompatibilityIssue.TypeAttributesMismatch;
        }
        if (observation.ExtendsMetadataToken != candidate.ExtendsMetadataToken)
        {
            return MetadataW7TypeDefinitionCompatibilityIssue.ExtendsMetadataTokenMismatch;
        }
        if (observation.FieldListRowId != candidate.FieldListRowId)
        {
            return MetadataW7TypeDefinitionCompatibilityIssue.FieldListStartMismatch;
        }
        if (row.FieldListEndExclusiveRowId != candidate.FieldListEndExclusiveRowId)
        {
            return MetadataW7TypeDefinitionCompatibilityIssue.FieldListEndMismatch;
        }
        if (observation.MethodListRowId != candidate.MethodListRowId)
        {
            return MetadataW7TypeDefinitionCompatibilityIssue.MethodListStartMismatch;
        }
        if (row.MethodListEndExclusiveRowId != candidate.MethodListEndExclusiveRowId)
        {
            return MetadataW7TypeDefinitionCompatibilityIssue.MethodListEndMismatch;
        }
        if (authorityTypeDefinition.TotalGenericArity != candidate.GenericParameterCount)
        {
            return MetadataW7TypeDefinitionCompatibilityIssue.GenericParameterCountMismatch;
        }
        return MetadataW7TypeDefinitionCompatibilityIssue.None;
    }

    private static bool EnclosingChainMatches(
        MetadataDefinitionAuthorityCatalogIdentity definitionAuthority,
        MetadataTypeDefinitionAuthorityIdentity authorityTypeDefinition,
        StaticFieldTypeDefinitionIdentity candidate,
        ImmutableArray<StaticFieldTypeDefinitionIdentity?> candidates)
    {
        var authorityCurrent = authorityTypeDefinition;
        var candidateCurrent = candidate;
        while (authorityCurrent.EnclosingTypeDefinitionToken.HasValue || candidateCurrent.EnclosingType is not null)
        {
            if (authorityCurrent.EnclosingTypeDefinitionToken is not { } enclosingToken ||
                candidateCurrent.EnclosingType is not { } candidateParent)
            {
                return false;
            }

            var authorityParent = definitionAuthority.ExactTypeDefinitionOrDefault(enclosingToken);
            if (authorityParent is null)
            {
                throw new InvalidOperationException(
                    "An exact definition authority must resolve every enclosing TypeDef token.");
            }
            var parentRowId = CanonicalReplayEncoding.MetadataTokenRowId(enclosingToken);
            var catalogCandidateParent = candidates[parentRowId - 1];
            if (catalogCandidateParent is null ||
                !catalogCandidateParent.Equals(candidateParent) ||
                CompareScalarFields(authorityParent, candidateParent) !=
                    MetadataW7TypeDefinitionCompatibilityIssue.None ||
                !DirectIntervalMatches(
                    authorityParent.FieldDefinitionTokens,
                    candidateParent.FieldListRowId,
                    candidateParent.FieldListEndExclusiveRowId,
                    tableId: 0x04) ||
                !DirectIntervalMatches(
                    authorityParent.MethodDefinitionTokens,
                    candidateParent.MethodListRowId,
                    candidateParent.MethodListEndExclusiveRowId,
                    tableId: 0x06))
            {
                return false;
            }

            authorityCurrent = authorityParent;
            candidateCurrent = candidateParent;
        }
        return true;
    }

    private static bool DirectIntervalMatches(
        ImmutableArray<int> authorityTokens,
        int startRowId,
        int endExclusiveRowId,
        int tableId)
    {
        if (endExclusiveRowId - startRowId != authorityTokens.Length)
        {
            return false;
        }
        for (var index = 0; index < authorityTokens.Length; index++)
        {
            if (authorityTokens[index] != ((tableId << 24) | (startRowId + index)))
            {
                return false;
            }
        }
        return true;
    }

    private static MetadataW7TypeDefinitionCompatibilityCatalogIdentity Stopped(
        MetadataW7TypeDefinitionCompatibilityCatalogResultKind resultKind,
        MetadataW7TypeDefinitionCompatibilityCatalogIssue issue,
        MetadataDefinitionAuthorityCatalogIdentity definitionAuthority,
        EvaluationDeterministicBound? reachedBound,
        int observedCount,
        int? relatedMetadataToken) =>
        new(
            resultKind,
            issue,
            definitionAuthority,
            ImmutableArray<MetadataW7TypeDefinitionCompatibilityCertificateIdentity>.Empty,
            reachedBound,
            observedCount,
            relatedMetadataToken);
}
