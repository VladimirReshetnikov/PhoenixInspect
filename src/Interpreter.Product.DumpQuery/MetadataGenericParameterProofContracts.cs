using System.Collections.Immutable;
using Interpreter.Core.Abstractions;

namespace Interpreter.Product.DumpQuery;

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

/// <summary>Freezes the shared Core MethodDef grammar certificate fields needed to prove generic method arity.</summary>
/// <remarks>This sealed draft identity can be minted only after complete shared-grammar consumption.</remarks>
public sealed class MetadataGenericMethodDeclarationCertificateIdentity :
    IEquatable<MetadataGenericMethodDeclarationCertificateIdentity>
{
    private const string CanonicalDomain = "metadata-v2-generic-method-declaration-certificate";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataGenericMethodDeclarationCertificateIdentity(
        StaticFieldMethodDefinitionIdentity methodDefinition,
        string signatureSha256,
        int signatureByteCount,
        int callingConvention,
        bool hasThis,
        bool hasExplicitThis,
        int genericParameterCount,
        int parameterCount,
        int aggregateTypeCount,
        int maximumObservedDepth)
    {
        MethodDefinition = methodDefinition;
        SignatureSha256 = signatureSha256;
        SignatureByteCount = signatureByteCount;
        CallingConvention = callingConvention;
        HasThis = hasThis;
        HasExplicitThis = hasExplicitThis;
        GenericParameterCount = genericParameterCount;
        ParameterCount = parameterCount;
        AggregateTypeCount = aggregateTypeCount;
        MaximumObservedDepth = maximumObservedDepth;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteLengthPrefixedBytes(methodDefinition.CanonicalBytes.AsSpan());
        writer.WriteSha256(signatureSha256, nameof(signatureSha256));
        writer.WriteInt32(signatureByteCount);
        writer.WriteInt32(callingConvention);
        writer.WriteBoolean(hasThis);
        writer.WriteBoolean(hasExplicitThis);
        writer.WriteInt32(genericParameterCount);
        writer.WriteInt32(parameterCount);
        writer.WriteInt32(aggregateTypeCount);
        writer.WriteInt32(maximumObservedDepth);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact MethodDef row whose signature was consumed.</summary>
    public StaticFieldMethodDefinitionIdentity MethodDefinition { get; }
    /// <summary>Gets the SHA-256 digest of the complete MethodDef signature bytes.</summary>
    public string SignatureSha256 { get; }
    /// <summary>Gets the exact complete signature byte count consumed by shared Core grammar.</summary>
    public int SignatureByteCount { get; }
    /// <summary>Gets the decoded low-nibble MethodDef calling convention.</summary>
    public int CallingConvention { get; }
    /// <summary>Gets the exact decoded HASTHIS bit.</summary>
    public bool HasThis { get; }
    /// <summary>Gets the exact decoded EXPLICITTHIS bit.</summary>
    public bool HasExplicitThis { get; }
    /// <summary>Gets the exact declared generic method arity.</summary>
    public int GenericParameterCount { get; }
    /// <summary>Gets the exact declared MethodDef parameter count.</summary>
    public int ParameterCount { get; }
    /// <summary>Gets the complete shared-grammar type-node count.</summary>
    public int AggregateTypeCount { get; }
    /// <summary>Gets the maximum recursive depth observed by shared Core grammar.</summary>
    public int MaximumObservedDepth { get; }
    /// <summary>Gets a defensive copy of the versioned canonical draft bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);
    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Tests canonical equality between two shared-grammar draft certificates.</summary>
    /// <param name="other">The other draft certificate.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(MetadataGenericMethodDeclarationCertificateIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);
    /// <summary>Tests draft canonical equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only when the object has identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataGenericMethodDeclarationCertificateIdentity);
    /// <summary>Computes a hash code from the immutable draft canonical bytes.</summary>
    /// <returns>A deterministic hash code for canonical draft content.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static MetadataGenericMethodDeclarationCertificateIdentity CreateFromSharedGrammar(
        StaticFieldMethodDefinitionIdentity methodDefinition,
        BoundedEcmaSignatureCertificate certificate)
    {
        var signature = methodDefinition.Signature;
        return new MetadataGenericMethodDeclarationCertificateIdentity(
            methodDefinition,
            CanonicalReplayEncoding.ComputeSha256(signature.AsSpan()),
            certificate.SignatureByteCount,
            certificate.CallingConvention,
            certificate.HasThis,
            certificate.HasExplicitThis,
            certificate.GenericParameterCount,
            certificate.ParameterCount,
            certificate.Counters.AggregateTypeCount,
            certificate.Counters.MaximumObservedDepth);
    }
}

/// <summary>Freezes exact or typed non-exact declared arity for one TypeDef or MethodDef GenericParam owner.</summary>
/// <remarks>
/// This sealed draft identity reads TypeDef arity from the raw row and MethodDef arity only from shared Core grammar.
/// A per-owner cap stop retains the owner and cap-plus-one observation but no usable declared arity.
/// </remarks>
public sealed class MetadataGenericParameterOwnerDeclarationIdentity :
    IEquatable<MetadataGenericParameterOwnerDeclarationIdentity>
{
    private const string CanonicalDomain = "metadata-v2-generic-parameter-owner-declaration";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataGenericParameterOwnerDeclarationIdentity(
        MetadataGenericParameterProofResultKind resultKind,
        MetadataGenericParameterProofIssue issue,
        MetadataSourceEndIdentity sourceEnds,
        MetadataGenericParameterOwnerIdentity owner,
        int? declaredArity,
        MetadataGenericMethodDeclarationCertificateIdentity? methodSignatureCertificate,
        EvaluationDeterministicBound? reachedBound,
        int observedCount)
    {
        ResultKind = resultKind;
        Issue = issue;
        SourceEnds = sourceEnds;
        Owner = owner;
        DeclaredArity = declaredArity;
        MethodSignatureCertificate = methodSignatureCertificate;
        ReachedBound = reachedBound;
        ObservedCount = observedCount;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32((int)resultKind);
        writer.WriteInt32((int)issue);
        writer.WriteLengthPrefixedBytes(sourceEnds.CanonicalBytes.AsSpan());
        writer.WriteLengthPrefixedBytes(owner.CanonicalBytes.AsSpan());
        ExpressionV2ContractEncoding.WriteOptionalInt32(writer, declaredArity);
        writer.WriteBoolean(methodSignatureCertificate is not null);
        if (methodSignatureCertificate is not null)
        {
            writer.WriteLengthPrefixedBytes(methodSignatureCertificate.CanonicalBytes.AsSpan());
        }
        ExpressionV2ContractEncoding.WriteOptionalBound(writer, reachedBound);
        writer.WriteInt32(observedCount);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets whether the complete draft owner declaration is exact, non-exact, or invalid.</summary>
    public MetadataGenericParameterProofResultKind ResultKind { get; }
    /// <summary>Gets the typed draft declaration issue, or none.</summary>
    public MetadataGenericParameterProofIssue Issue { get; }
    /// <summary>Gets the exact metadata source ends proving the owner token domain.</summary>
    public MetadataSourceEndIdentity SourceEnds { get; }
    /// <summary>Gets the exact physical TypeDef-or-MethodDef owner.</summary>
    public MetadataGenericParameterOwnerIdentity Owner { get; }
    /// <summary>Gets the exact declared arity only for an exact outcome, including zero.</summary>
    public int? DeclaredArity { get; }
    /// <summary>Gets shared Core MethodDef grammar evidence only for an exact method declaration.</summary>
    public MetadataGenericMethodDeclarationCertificateIdentity? MethodSignatureCertificate { get; }
    /// <summary>Gets the per-owner arity bound only for a cap-plus-one outcome.</summary>
    public EvaluationDeterministicBound? ReachedBound { get; }
    /// <summary>Gets the exact cap-plus-one count, otherwise zero.</summary>
    public int ObservedCount { get; }
    /// <summary>Gets a defensive copy of the versioned canonical draft bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);
    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one draft declaration from the exact raw TypeDef GenericParam count.</summary>
    /// <param name="sourceEnds">The exact metadata source ends containing the owner TypeDef.</param>
    /// <param name="typeDefinition">The complete raw TypeDef row and exact arity count.</param>
    /// <returns>An exact zero-to-cap declaration or prefix-free cap-plus-one draft outcome.</returns>
    public static MetadataGenericParameterOwnerDeclarationIdentity FromTypeDefinition(
        MetadataSourceEndIdentity sourceEnds,
        MetadataRawTypeDefinitionIdentity typeDefinition)
    {
        ArgumentNullException.ThrowIfNull(sourceEnds);
        ArgumentNullException.ThrowIfNull(typeDefinition);
        var owner = MetadataGenericParameterOwnerIdentity.ForTypeDefinition(typeDefinition);
        var rowId = typeDefinition.TypeDefinitionToken & 0x00FF_FFFF;
        if (!typeDefinition.MetadataModule.Equals(sourceEnds.SourceModule))
        {
            return Invalid(sourceEnds, owner, MetadataGenericParameterProofIssue.SourceModuleMismatch);
        }
        if (rowId == 0 || rowId > sourceEnds.TypeDefinitionRowCount)
        {
            return Invalid(sourceEnds, owner, MetadataGenericParameterProofIssue.OwnerTokenOutOfRange);
        }
        return typeDefinition.GenericParameterCount > StaticFieldV2Limits.MaximumGenericParameterCount
            ? NonExactArity(sourceEnds, owner)
            : Exact(sourceEnds, owner, typeDefinition.GenericParameterCount, null);
    }

    /// <summary>Creates one draft declaration by fully decoding the exact MethodDef signature with shared Core grammar.</summary>
    /// <param name="sourceEnds">The exact metadata source ends containing the owner MethodDef.</param>
    /// <param name="methodDefinition">The complete MethodDef row, attributes, owner, and signature.</param>
    /// <returns>An exact, invalid, or prefix-free cap-plus-one draft declaration.</returns>
    public static MetadataGenericParameterOwnerDeclarationIdentity FromMethodDefinition(
        MetadataSourceEndIdentity sourceEnds,
        StaticFieldMethodDefinitionIdentity methodDefinition)
    {
        ArgumentNullException.ThrowIfNull(sourceEnds);
        ArgumentNullException.ThrowIfNull(methodDefinition);
        var owner = MetadataGenericParameterOwnerIdentity.ForMethodDefinition(methodDefinition);
        var rowId = methodDefinition.MethodDefinitionToken & 0x00FF_FFFF;
        if (!methodDefinition.DeclaringType.MetadataModule.Equals(sourceEnds.SourceModule))
        {
            return Invalid(sourceEnds, owner, MetadataGenericParameterProofIssue.SourceModuleMismatch);
        }
        if (rowId == 0 || rowId > sourceEnds.MethodDefinitionRowCount)
        {
            return Invalid(sourceEnds, owner, MetadataGenericParameterProofIssue.OwnerTokenOutOfRange);
        }
        var signature = methodDefinition.Signature;
        var outcome = BoundedEcmaSignatureProjection.Decode(
            signature.AsSpan(),
            BoundedEcmaSignatureForm.MethodDefinition,
            new BoundedEcmaSignatureLimits(
                StaticFieldMethodDefinitionIdentity.MaximumMethodSignatureLength,
                int.MaxValue,
                int.MaxValue,
                int.MaxValue,
                StaticFieldV2Limits.MaximumGenericParameterCount,
                int.MaxValue,
                int.MaxValue,
                int.MaxValue));
        if (outcome.Kind == BoundedEcmaSignatureDecodeKind.BoundReached &&
            outcome.ReachedBound == BoundedEcmaSignatureBoundKind.GenericParameterCount)
        {
            return NonExactArity(sourceEnds, owner);
        }
        if (!outcome.IsExact || outcome.Certificate is not { } certificate)
        {
            return Invalid(sourceEnds, owner, MetadataGenericParameterProofIssue.MethodSignatureInvalid);
        }

        var isStatic = (methodDefinition.Attributes & (int)System.Reflection.MethodAttributes.Static) != 0;
        if (certificate.HasThis == isStatic)
        {
            return Invalid(sourceEnds, owner, MetadataGenericParameterProofIssue.MethodReceiverMismatch);
        }
        var wrappedCertificate =
            MetadataGenericMethodDeclarationCertificateIdentity.CreateFromSharedGrammar(methodDefinition, certificate);
        return Exact(sourceEnds, owner, certificate.GenericParameterCount, wrappedCertificate);
    }

    /// <summary>Tests canonical equality between two owner-declaration draft identities.</summary>
    /// <param name="other">The other draft declaration.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(MetadataGenericParameterOwnerDeclarationIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);
    /// <summary>Tests draft canonical equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only when the object has identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataGenericParameterOwnerDeclarationIdentity);
    /// <summary>Computes a hash code from the immutable draft canonical bytes.</summary>
    /// <returns>A deterministic hash code for canonical draft content.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    private static MetadataGenericParameterOwnerDeclarationIdentity Exact(
        MetadataSourceEndIdentity sourceEnds,
        MetadataGenericParameterOwnerIdentity owner,
        int declaredArity,
        MetadataGenericMethodDeclarationCertificateIdentity? certificate) =>
        new(
            MetadataGenericParameterProofResultKind.Exact,
            MetadataGenericParameterProofIssue.None,
            sourceEnds,
            owner,
            declaredArity,
            certificate,
            null,
            0);

    private static MetadataGenericParameterOwnerDeclarationIdentity NonExactArity(
        MetadataSourceEndIdentity sourceEnds,
        MetadataGenericParameterOwnerIdentity owner) =>
        new(
            MetadataGenericParameterProofResultKind.NonExact,
            MetadataGenericParameterProofIssue.OwnerArityBoundReached,
            sourceEnds,
            owner,
            null,
            null,
            new EvaluationDeterministicBound(
                ExpressionV2ContractLimits.GenericParameterCountBoundName,
                StaticFieldV2Limits.MaximumGenericParameterCount),
            StaticFieldV2Limits.MaximumGenericParameterCount + 1);

    private static MetadataGenericParameterOwnerDeclarationIdentity Invalid(
        MetadataSourceEndIdentity sourceEnds,
        MetadataGenericParameterOwnerIdentity owner,
        MetadataGenericParameterProofIssue issue) =>
        new(MetadataGenericParameterProofResultKind.Invalid, issue, sourceEnds, owner, null, null, null, 0);

}

/// <summary>Freezes complete module-wide GenericParam table acquisition in exact physical RID order.</summary>
/// <remarks>
/// This sealed draft catalog requires exact source ends. Bound, incomplete, and invalid outcomes expose no row prefix;
/// only an exact catalog retains the complete normalized physical table.
/// </remarks>
public sealed class MetadataGenericParameterTableCatalogIdentity :
    IEquatable<MetadataGenericParameterTableCatalogIdentity>
{
    private const string CanonicalDomain = "metadata-v2-generic-parameter-table-catalog";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<MetadataGenericParameterIdentity> rows;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataGenericParameterTableCatalogIdentity(
        MetadataGenericParameterProofResultKind resultKind,
        MetadataGenericParameterProofIssue issue,
        MetadataSourceEndIdentity sourceEnds,
        ImmutableArray<MetadataGenericParameterIdentity> rows,
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
        ExpressionV2ContractEncoding.WriteOptionalBound(writer, reachedBound);
        writer.WriteInt32(observedCount);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets whether the complete draft table acquisition is exact, non-exact, or invalid.</summary>
    public MetadataGenericParameterProofResultKind ResultKind { get; }
    /// <summary>Gets the typed draft table issue, or none.</summary>
    public MetadataGenericParameterProofIssue Issue { get; }
    /// <summary>Gets exact source ends for the metadata module containing the GenericParam table.</summary>
    public MetadataSourceEndIdentity SourceEnds { get; }
    /// <summary>Gets a defensive copy of the complete physical rows only for an exact draft catalog.</summary>
    public ImmutableArray<MetadataGenericParameterIdentity> Rows =>
        ExpressionV2ContractEncoding.Copy(rows);
    /// <summary>Gets the global GenericParam table bound only after a cap-plus-one observation.</summary>
    public EvaluationDeterministicBound? ReachedBound { get; }
    /// <summary>Gets cap-plus-one or the acquired input count for a non-exact/invalid result, otherwise zero.</summary>
    public int ObservedCount { get; }
    /// <summary>Gets a defensive copy of the versioned canonical draft bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);
    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one complete module-wide GenericParam draft catalog from exact source ends.</summary>
    /// <param name="sourceEnds">The exact table ends for the source metadata module.</param>
    /// <param name="rows">Every physical GenericParam row in RID order; default denotes incomplete acquisition.</param>
    /// <returns>An exact normalized, prefix-free non-exact, or typed invalid draft catalog.</returns>
    public static MetadataGenericParameterTableCatalogIdentity Create(
        MetadataSourceEndIdentity sourceEnds,
        ImmutableArray<MetadataGenericParameterIdentity> rows)
    {
        ArgumentNullException.ThrowIfNull(sourceEnds);
        var sourceCount = sourceEnds.GenericParameterRowCount;
        if (sourceCount > StaticFieldV2Limits.MaximumGenericParameterRowCount)
        {
            return new MetadataGenericParameterTableCatalogIdentity(
                MetadataGenericParameterProofResultKind.NonExact,
                MetadataGenericParameterProofIssue.TableRowBoundReached,
                sourceEnds,
                ImmutableArray<MetadataGenericParameterIdentity>.Empty,
                new EvaluationDeterministicBound(
                    ExpressionV2ContractLimits.GenericParameterRowCountBoundName,
                    StaticFieldV2Limits.MaximumGenericParameterRowCount),
                StaticFieldV2Limits.MaximumGenericParameterRowCount + 1);
        }
        if (rows.IsDefault || rows.Length < sourceCount)
        {
            return new MetadataGenericParameterTableCatalogIdentity(
                MetadataGenericParameterProofResultKind.NonExact,
                MetadataGenericParameterProofIssue.TableIncomplete,
                sourceEnds,
                ImmutableArray<MetadataGenericParameterIdentity>.Empty,
                null,
                rows.IsDefault ? 0 : rows.Length);
        }
        if (rows.Length > sourceCount)
        {
            return new MetadataGenericParameterTableCatalogIdentity(
                MetadataGenericParameterProofResultKind.Invalid,
                MetadataGenericParameterProofIssue.TableRowCountConflict,
                sourceEnds,
                ImmutableArray<MetadataGenericParameterIdentity>.Empty,
                null,
                rows.Length);
        }

        var copiedRows = ImmutableArray.CreateRange(rows);
        var ownerPositions = new HashSet<(string OwnerSha256, int Position)>();
        var previousOwnerKey = -1L;
        MetadataGenericParameterOwnerIdentity? previousOwner = null;
        for (var index = 0; index < copiedRows.Length; index++)
        {
            var row = copiedRows[index];
            if (row is null)
            {
                return Invalid(sourceEnds, MetadataGenericParameterProofIssue.TableRowCountConflict, copiedRows.Length);
            }
            var expectedToken = 0x2A000000 | checked(index + 1);
            if (row.GenericParameterToken != expectedToken)
            {
                return Invalid(sourceEnds, MetadataGenericParameterProofIssue.PhysicalOrderInvalid, copiedRows.Length);
            }
            if (!row.Owner.MetadataModule.Equals(sourceEnds.SourceModule))
            {
                return Invalid(sourceEnds, MetadataGenericParameterProofIssue.SourceModuleMismatch, copiedRows.Length);
            }
            if (!OwnerTokenIsInRange(sourceEnds, row.Owner))
            {
                return Invalid(sourceEnds, MetadataGenericParameterProofIssue.OwnerTokenOutOfRange, copiedRows.Length);
            }
            var ownerKey = TypeOrMethodDefinitionCodedIndex(row.Owner);
            if (ownerKey < previousOwnerKey ||
                ownerKey == previousOwnerKey && previousOwner is not null && !previousOwner.Equals(row.Owner))
            {
                return Invalid(sourceEnds, MetadataGenericParameterProofIssue.OwnerSortInvalid, copiedRows.Length);
            }
            if (!ownerPositions.Add((row.Owner.Sha256, row.Position)))
            {
                return Invalid(sourceEnds, MetadataGenericParameterProofIssue.DuplicateOwnerPosition, copiedRows.Length);
            }
            previousOwnerKey = ownerKey;
            previousOwner = row.Owner;
        }

        return new MetadataGenericParameterTableCatalogIdentity(
            MetadataGenericParameterProofResultKind.Exact,
            MetadataGenericParameterProofIssue.None,
            sourceEnds,
            copiedRows,
            null,
            0);
    }

    /// <summary>Tests canonical equality between two module-wide GenericParam draft catalogs.</summary>
    /// <param name="other">The other draft catalog.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(MetadataGenericParameterTableCatalogIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);
    /// <summary>Tests draft canonical equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only when the object has identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataGenericParameterTableCatalogIdentity);
    /// <summary>Computes a hash code from the immutable draft canonical bytes.</summary>
    /// <returns>A deterministic hash code for canonical draft content.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal ImmutableArray<MetadataGenericParameterIdentity> ExactRows => rows;

    private static MetadataGenericParameterTableCatalogIdentity Invalid(
        MetadataSourceEndIdentity sourceEnds,
        MetadataGenericParameterProofIssue issue,
        int observedCount) =>
        new(
            MetadataGenericParameterProofResultKind.Invalid,
            issue,
            sourceEnds,
            ImmutableArray<MetadataGenericParameterIdentity>.Empty,
            null,
            observedCount);

    private static bool OwnerTokenIsInRange(
        MetadataSourceEndIdentity sourceEnds,
        MetadataGenericParameterOwnerIdentity owner)
    {
        var rowId = owner.OwnerMetadataToken & 0x00FF_FFFF;
        return owner.Kind switch
        {
            MetadataGenericParameterOwnerKind.TypeDefinition =>
                rowId > 0 && rowId <= sourceEnds.TypeDefinitionRowCount,
            MetadataGenericParameterOwnerKind.MethodDefinition =>
                rowId > 0 && rowId <= sourceEnds.MethodDefinitionRowCount,
            _ => false,
        };
    }

    private static long TypeOrMethodDefinitionCodedIndex(MetadataGenericParameterOwnerIdentity owner)
    {
        var rowId = owner.OwnerMetadataToken & 0x00FF_FFFF;
        var tag = owner.Kind == MetadataGenericParameterOwnerKind.MethodDefinition ? 1 : 0;
        return ((long)(uint)rowId << 1) | (uint)tag;
    }
}

/// <summary>Freezes the complete GenericParam row set selected for one exact owner declaration.</summary>
/// <remarks>
/// This sealed draft proof selects from a complete module catalog and normalizes exact rows by declared position.
/// Non-exact and invalid results expose no selected row prefix; exact zero arity exposes an initialized empty set.
/// </remarks>
public sealed class MetadataGenericParameterOwnerSetIdentity :
    IEquatable<MetadataGenericParameterOwnerSetIdentity>
{
    private const string CanonicalDomain = "metadata-v2-generic-parameter-owner-set";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<MetadataGenericParameterIdentity> parameters;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataGenericParameterOwnerSetIdentity(
        MetadataGenericParameterProofResultKind resultKind,
        MetadataGenericParameterProofIssue issue,
        MetadataGenericParameterOwnerDeclarationIdentity declaration,
        MetadataGenericParameterTableCatalogIdentity tableCatalog,
        ImmutableArray<MetadataGenericParameterIdentity> parameters,
        EvaluationDeterministicBound? reachedBound,
        int observedCount)
    {
        ResultKind = resultKind;
        Issue = issue;
        Declaration = declaration;
        TableCatalog = tableCatalog;
        this.parameters = parameters;
        ReachedBound = reachedBound;
        ObservedCount = observedCount;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32((int)resultKind);
        writer.WriteInt32((int)issue);
        writer.WriteLengthPrefixedBytes(declaration.CanonicalBytes.AsSpan());
        writer.WriteLengthPrefixedBytes(tableCatalog.CanonicalBytes.AsSpan());
        ExpressionV2ContractEncoding.WriteCanonicalArray(writer, parameters, static parameter => parameter.CanonicalBytes);
        ExpressionV2ContractEncoding.WriteOptionalBound(writer, reachedBound);
        writer.WriteInt32(observedCount);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets whether the complete draft owner selection is exact, non-exact, or invalid.</summary>
    public MetadataGenericParameterProofResultKind ResultKind { get; }
    /// <summary>Gets the typed draft owner-selection issue, or none.</summary>
    public MetadataGenericParameterProofIssue Issue { get; }
    /// <summary>Gets the exact or typed non-exact owner declaration used for selection.</summary>
    public MetadataGenericParameterOwnerDeclarationIdentity Declaration { get; }
    /// <summary>Gets the complete module-wide GenericParam draft catalog used for selection.</summary>
    public MetadataGenericParameterTableCatalogIdentity TableCatalog { get; }
    /// <summary>Gets a defensive copy of exact selected rows normalized by declared position.</summary>
    public ImmutableArray<MetadataGenericParameterIdentity> Parameters =>
        ExpressionV2ContractEncoding.Copy(parameters);
    /// <summary>Gets a propagated operation bound only for a non-exact draft selection.</summary>
    public EvaluationDeterministicBound? ReachedBound { get; }
    /// <summary>Gets a propagated cap-plus-one or incomplete count, otherwise zero.</summary>
    public int ObservedCount { get; }
    /// <summary>Gets a defensive copy of the versioned canonical draft bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);
    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Selects one complete declared owner row set from a module-wide GenericParam draft catalog.</summary>
    /// <param name="declaration">The exact or typed non-exact owner arity declaration.</param>
    /// <param name="tableCatalog">The complete module-wide GenericParam table outcome.</param>
    /// <returns>An exact position-normalized, prefix-free non-exact, or typed invalid draft owner set.</returns>
    public static MetadataGenericParameterOwnerSetIdentity Create(
        MetadataGenericParameterOwnerDeclarationIdentity declaration,
        MetadataGenericParameterTableCatalogIdentity tableCatalog)
    {
        ArgumentNullException.ThrowIfNull(declaration);
        ArgumentNullException.ThrowIfNull(tableCatalog);
        if (declaration.ResultKind == MetadataGenericParameterProofResultKind.NonExact)
        {
            return NonExact(declaration, tableCatalog, declaration.Issue, declaration.ReachedBound, declaration.ObservedCount);
        }
        if (tableCatalog.ResultKind == MetadataGenericParameterProofResultKind.NonExact)
        {
            return NonExact(declaration, tableCatalog, tableCatalog.Issue,
                tableCatalog.ReachedBound, tableCatalog.ObservedCount);
        }
        if (declaration.ResultKind == MetadataGenericParameterProofResultKind.Invalid)
        {
            return Invalid(declaration, tableCatalog, MetadataGenericParameterProofIssue.OwnerDeclarationInvalid);
        }
        if (tableCatalog.ResultKind == MetadataGenericParameterProofResultKind.Invalid)
        {
            return Invalid(declaration, tableCatalog, tableCatalog.Issue);
        }
        if (!declaration.SourceEnds.Equals(tableCatalog.SourceEnds))
        {
            return Invalid(declaration, tableCatalog, MetadataGenericParameterProofIssue.SourceModuleMismatch);
        }

        var selected = tableCatalog.ExactRows
            .Where(row => row.Owner.Equals(declaration.Owner))
            .OrderBy(static row => row.Position)
            .ToImmutableArray();
        var declaredArity = declaration.DeclaredArity!.Value;
        if (selected.Length != declaredArity ||
            selected.Where((row, index) => row.Position != index).Any())
        {
            return Invalid(declaration, tableCatalog, MetadataGenericParameterProofIssue.OwnerPositionCoverageInvalid);
        }

        return new MetadataGenericParameterOwnerSetIdentity(
            MetadataGenericParameterProofResultKind.Exact,
            MetadataGenericParameterProofIssue.None,
            declaration,
            tableCatalog,
            selected,
            null,
            0);
    }

    /// <summary>Tests canonical equality between two selected-owner GenericParam draft identities.</summary>
    /// <param name="other">The other draft owner set.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(MetadataGenericParameterOwnerSetIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);
    /// <summary>Tests draft canonical equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only when the object has identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataGenericParameterOwnerSetIdentity);
    /// <summary>Computes a hash code from the immutable draft canonical bytes.</summary>
    /// <returns>A deterministic hash code for canonical draft content.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal ImmutableArray<MetadataGenericParameterIdentity> ExactParameters => parameters;

    private static MetadataGenericParameterOwnerSetIdentity NonExact(
        MetadataGenericParameterOwnerDeclarationIdentity declaration,
        MetadataGenericParameterTableCatalogIdentity tableCatalog,
        MetadataGenericParameterProofIssue issue,
        EvaluationDeterministicBound? bound,
        int observedCount) =>
        new(
            MetadataGenericParameterProofResultKind.NonExact,
            issue,
            declaration,
            tableCatalog,
            ImmutableArray<MetadataGenericParameterIdentity>.Empty,
            bound,
            observedCount);

    private static MetadataGenericParameterOwnerSetIdentity Invalid(
        MetadataGenericParameterOwnerDeclarationIdentity declaration,
        MetadataGenericParameterTableCatalogIdentity tableCatalog,
        MetadataGenericParameterProofIssue issue) =>
        new(
            MetadataGenericParameterProofResultKind.Invalid,
            issue,
            declaration,
            tableCatalog,
            ImmutableArray<MetadataGenericParameterIdentity>.Empty,
            null,
            0);
}

/// <summary>Freezes one complete row-addressed Exact/Unavailable binding ledger for a selected GenericParam owner.</summary>
/// <remarks>
/// This sealed draft ledger matches bindings by exact physical row identity, then normalizes them by proven declared
/// position. Caller array position never supplies a GenericParam position guess.
/// </remarks>
public sealed class MetadataGenericParameterBindingLedgerIdentity :
    IEquatable<MetadataGenericParameterBindingLedgerIdentity>
{
    private const string CanonicalDomain = "metadata-v2-generic-parameter-binding-ledger";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<MetadataTypeArgumentBindingIdentity> bindings;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataGenericParameterBindingLedgerIdentity(
        MetadataGenericParameterProofResultKind resultKind,
        MetadataGenericParameterProofIssue issue,
        MetadataGenericParameterOwnerSetIdentity parameterSet,
        ImmutableArray<MetadataTypeArgumentBindingIdentity> bindings)
    {
        ResultKind = resultKind;
        Issue = issue;
        ParameterSet = parameterSet;
        this.bindings = bindings;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32((int)resultKind);
        writer.WriteInt32((int)issue);
        writer.WriteLengthPrefixedBytes(parameterSet.CanonicalBytes.AsSpan());
        ExpressionV2ContractEncoding.WriteCanonicalArray(writer, bindings, static binding => binding.CanonicalBytes);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets whether the complete draft binding ledger is exact, non-exact, or invalid.</summary>
    public MetadataGenericParameterProofResultKind ResultKind { get; }
    /// <summary>Gets the typed draft ledger issue, or none.</summary>
    public MetadataGenericParameterProofIssue Issue { get; }
    /// <summary>Gets the exact or typed non-exact selected-owner row proof.</summary>
    public MetadataGenericParameterOwnerSetIdentity ParameterSet { get; }
    /// <summary>Gets a defensive copy of exact bindings normalized by proven declared position.</summary>
    public ImmutableArray<MetadataTypeArgumentBindingIdentity> Bindings =>
        ExpressionV2ContractEncoding.Copy(bindings);
    /// <summary>Gets a defensive copy of the versioned canonical draft bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);
    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one complete row-addressed Exact/Unavailable binding draft ledger.</summary>
    /// <param name="parameterSet">The complete selected-owner GenericParam row proof.</param>
    /// <param name="bindings">One exact-row binding per selected row in any caller order.</param>
    /// <returns>An exact normalized, prefix-free non-exact, or typed invalid draft binding ledger.</returns>
    public static MetadataGenericParameterBindingLedgerIdentity Create(
        MetadataGenericParameterOwnerSetIdentity parameterSet,
        ImmutableArray<MetadataTypeArgumentBindingIdentity> bindings)
    {
        ArgumentNullException.ThrowIfNull(parameterSet);
        if (parameterSet.ResultKind != MetadataGenericParameterProofResultKind.Exact)
        {
            return new MetadataGenericParameterBindingLedgerIdentity(
                parameterSet.ResultKind,
                parameterSet.Issue,
                parameterSet,
                ImmutableArray<MetadataTypeArgumentBindingIdentity>.Empty);
        }
        var parameters = parameterSet.ExactParameters;
        if (bindings.IsDefault || bindings.Length < parameters.Length)
        {
            return new MetadataGenericParameterBindingLedgerIdentity(
                MetadataGenericParameterProofResultKind.NonExact,
                MetadataGenericParameterProofIssue.BindingIncomplete,
                parameterSet,
                ImmutableArray<MetadataTypeArgumentBindingIdentity>.Empty);
        }
        if (bindings.Length > parameters.Length)
        {
            return Invalid(parameterSet, MetadataGenericParameterProofIssue.BindingRowMismatch);
        }

        var selectedByToken = parameters.ToDictionary(
            static parameter => parameter.GenericParameterToken,
            static parameter => parameter);
        var bindingsByToken = new Dictionary<int, MetadataTypeArgumentBindingIdentity>();
        foreach (var binding in bindings)
        {
            if (binding is null ||
                !selectedByToken.TryGetValue(binding.Parameter.GenericParameterToken, out var selected) ||
                !selected.Equals(binding.Parameter))
            {
                return Invalid(parameterSet, MetadataGenericParameterProofIssue.BindingRowMismatch);
            }
            if (!bindingsByToken.TryAdd(binding.Parameter.GenericParameterToken, binding))
            {
                return Invalid(parameterSet, MetadataGenericParameterProofIssue.DuplicateBinding);
            }
        }
        if (bindingsByToken.Count != parameters.Length)
        {
            return new MetadataGenericParameterBindingLedgerIdentity(
                MetadataGenericParameterProofResultKind.NonExact,
                MetadataGenericParameterProofIssue.BindingIncomplete,
                parameterSet,
                ImmutableArray<MetadataTypeArgumentBindingIdentity>.Empty);
        }

        var normalized = parameters
            .Select(parameter => bindingsByToken[parameter.GenericParameterToken])
            .ToImmutableArray();
        return new MetadataGenericParameterBindingLedgerIdentity(
            MetadataGenericParameterProofResultKind.Exact,
            MetadataGenericParameterProofIssue.None,
            parameterSet,
            normalized);
    }

    /// <summary>Tests canonical equality between two row-addressed GenericParam binding draft ledgers.</summary>
    /// <param name="other">The other draft ledger.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(MetadataGenericParameterBindingLedgerIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);
    /// <summary>Tests draft canonical equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only when the object has identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataGenericParameterBindingLedgerIdentity);
    /// <summary>Computes a hash code from the immutable draft canonical bytes.</summary>
    /// <returns>A deterministic hash code for canonical draft content.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    private static MetadataGenericParameterBindingLedgerIdentity Invalid(
        MetadataGenericParameterOwnerSetIdentity parameterSet,
        MetadataGenericParameterProofIssue issue) =>
        new(
            MetadataGenericParameterProofResultKind.Invalid,
            issue,
            parameterSet,
            ImmutableArray<MetadataTypeArgumentBindingIdentity>.Empty);
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
