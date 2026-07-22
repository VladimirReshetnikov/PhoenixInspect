using System.Collections.Immutable;
using System.Reflection;
using Interpreter.Core.Abstractions;

namespace Interpreter.Product.DumpQuery;

/// <summary>Classifies one complete pointer-aware MethodDef declaration-table draft proof.</summary>
/// <remarks>Every non-exact or invalid draft result exposes no derived MethodDef row prefix.</remarks>
public enum MetadataMethodDefinitionTableResultKind
{
    /// <summary>Every physical MethodDef row, owner, receiver form, and signature grammar fact is exact.</summary>
    Exact = 1,

    /// <summary>A required complete source or signature operation stopped before exact projection.</summary>
    NonExact = 2,

    /// <summary>Complete supplied evidence contradicted source, ownership, receiver, or signature invariants.</summary>
    Invalid = 3,
}

/// <summary>Identifies the typed disposition of one MethodDef declaration-table draft proof.</summary>
/// <remarks>The draft issue remains available without retaining any derived row candidate.</remarks>
public enum MetadataMethodDefinitionTableIssue
{
    /// <summary>No issue applies to an exact complete catalog.</summary>
    None = 0,

    /// <summary>The pointer-aware TypeDef ownership catalog was non-exact.</summary>
    TypeDefinitionCatalogNonExact = 1,

    /// <summary>The pointer-aware TypeDef ownership catalog was invalid for a reason not classified below.</summary>
    TypeDefinitionCatalogInvalid = 2,

    /// <summary>The TypeDef ownership evidence did not cover every MethodDef exactly once.</summary>
    OwnershipMissing = 3,

    /// <summary>The MethodPtr ownership evidence selected one MethodDef more than once.</summary>
    OwnershipDuplicate = 4,

    /// <summary>An ownership token did not identify an in-range MethodDef row.</summary>
    OwnershipTargetOutOfRange = 5,

    /// <summary>The supplied MethodDef observations were unavailable or stopped before the exact source end.</summary>
    TableIncomplete = 6,

    /// <summary>The supplied MethodDef observation count exceeded the exact source end.</summary>
    TableRowCountConflict = 7,

    /// <summary>The supplied MethodDef tokens did not form the exact physical RID sequence.</summary>
    PhysicalOrderInvalid = 8,

    /// <summary>A physical MethodDef observation belonged to another metadata module.</summary>
    SourceModuleMismatch = 9,

    /// <summary>One exact MethodDef signature crossed the retained byte-count bound.</summary>
    SignatureByteBoundReached = 10,

    /// <summary>One exact MethodDef signature crossed the shared recursive type-depth bound.</summary>
    SignatureDepthBoundReached = 11,

    /// <summary>One exact MethodDef signature crossed the shared aggregate generic-argument bound.</summary>
    SignatureGenericArgumentBoundReached = 12,

    /// <summary>One exact MethodDef signature crossed the shared parameter-count bound.</summary>
    SignatureParameterBoundReached = 13,

    /// <summary>One exact MethodDef signature crossed the shared multidimensional-array rank bound.</summary>
    SignatureArrayRankBoundReached = 14,

    /// <summary>The bounded MethodDef signature prefix stopped before its exact source end was observed.</summary>
    SignatureIncomplete = 15,

    /// <summary>A complete MethodDef signature was structurally invalid under the shared grammar.</summary>
    SignatureInvalid = 16,

    /// <summary>A complete MethodDef signature contained bytes after its shared-grammar source end.</summary>
    SignatureTrailingData = 17,

    /// <summary>The signature receiver flag contradicted the physical MethodAttributes.Static bit.</summary>
    ReceiverMismatch = 18,
}

/// <summary>Freezes physical columns needed for one MethodDef generic-declaration draft projection.</summary>
/// <remarks>
/// This sealed draft observation retains every physical MethodDef table column after heap decoding plus a bounded
/// signature prefix and optional exact blob source end. ParamList is checked only against the physical simple-index
/// domain because current source ends do not yet cover Param or ParamPtr; their ordering and ownership semantics remain
/// deliberately unasserted. The observation carries no caller-authored declaring TypeDef; ownership is derived only
/// from the complete pointer-aware TypeDef catalog.
/// </remarks>
public sealed class MetadataMethodDefinitionRowObservationIdentity :
    IEquatable<MetadataMethodDefinitionRowObservationIdentity>
{
    /// <summary>Gets the maximum exact MethodDef signature byte count admitted by shared grammar projection.</summary>
    public const int MaximumSignatureByteCount = 256;

    /// <summary>Gets the stable draft bound name for one retained MethodDef signature.</summary>
    public const string MaximumSignatureByteCountBoundName = "metadata-v2.methoddef-signature.bytes";

    private const string CanonicalDomain = "metadata-v2-methoddef-row-observation";
    private const int CanonicalSchemaVersion = 1;
    private const int MaximumListStartRowId = 0x0100_0000;
    private readonly ImmutableArray<byte> signaturePrefixBytes;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataMethodDefinitionRowObservationIdentity(
        StaticFieldMetadataModuleIdentity metadataModule,
        int methodDefinitionToken,
        int relativeVirtualAddress,
        int implementationAttributes,
        int attributes,
        string name,
        ImmutableArray<byte> signaturePrefixBytes,
        int? signatureByteCount,
        int parameterListRowId)
    {
        MetadataModule = metadataModule;
        MethodDefinitionToken = methodDefinitionToken;
        RelativeVirtualAddress = relativeVirtualAddress;
        ImplementationAttributes = implementationAttributes;
        Attributes = attributes;
        Name = name;
        this.signaturePrefixBytes = signaturePrefixBytes;
        SignatureByteCount = signatureByteCount;
        ParameterListRowId = parameterListRowId;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteLengthPrefixedBytes(metadataModule.CanonicalBytes.AsSpan());
        writer.WriteInt32(methodDefinitionToken);
        writer.WriteInt32(relativeVirtualAddress);
        writer.WriteInt32(implementationAttributes);
        writer.WriteInt32(attributes);
        writer.WriteString(name);
        writer.WriteLengthPrefixedBytes(signaturePrefixBytes.AsSpan());
        ExpressionV2ContractEncoding.WriteOptionalInt32(writer, signatureByteCount);
        writer.WriteInt32(parameterListRowId);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact metadata module containing the physical draft MethodDef row.</summary>
    public StaticFieldMetadataModuleIdentity MetadataModule { get; }

    /// <summary>Gets the exact non-nil physical draft MethodDef token.</summary>
    public int MethodDefinitionToken { get; }

    /// <summary>Gets the exact raw physical draft MethodDef RVA.</summary>
    public int RelativeVirtualAddress { get; }

    /// <summary>Gets the exact raw physical draft MethodImplAttributes bits.</summary>
    public int ImplementationAttributes { get; }

    /// <summary>Gets the exact raw physical draft MethodAttributes bits.</summary>
    public int Attributes { get; }

    /// <summary>Gets the exact decoded bounded physical draft MethodDef name, including empty.</summary>
    public string Name { get; }

    /// <summary>Gets whether the physical draft row has MethodAttributes.Static.</summary>
    public bool IsStatic => (Attributes & (int)MethodAttributes.Static) != 0;

    /// <summary>Gets a defensive copy of the bounded physical draft MethodDef signature prefix.</summary>
    /// <remarks>
    /// The prefix is complete when <see cref="SignatureByteCount"/> is at most
    /// <see cref="MaximumSignatureByteCount"/>. Otherwise it contains exactly cap plus one bytes.
    /// </remarks>
    public ImmutableArray<byte> SignaturePrefixBytes => ExpressionV2ContractEncoding.Copy(signaturePrefixBytes);

    /// <summary>Gets the exact complete signature length, or null when acquisition stopped before its source end.</summary>
    public int? SignatureByteCount { get; }

    /// <summary>Gets whether the exact complete signature length and source end were observed.</summary>
    public bool SignatureSourceEndObserved => SignatureByteCount.HasValue;

    /// <summary>Gets the raw physical draft MethodDef.ParamList starting row identifier.</summary>
    /// <remarks>Current draft source ends cannot yet prove Param or ParamPtr ordering semantics for this value.</remarks>
    public int ParameterListRowId { get; }

    /// <summary>Gets a defensive copy of the versioned canonical draft observation bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft observation.</summary>
    public string Sha256 { get; }

    internal ImmutableArray<byte> SignaturePrefixBytesCore => signaturePrefixBytes;

    /// <summary>Creates one physical-column-only MethodDef row draft observation.</summary>
    /// <param name="metadataModule">The exact metadata module containing the physical row.</param>
    /// <param name="methodDefinitionToken">The exact non-nil MethodDef token.</param>
    /// <param name="relativeVirtualAddress">The exact raw MethodDef RVA.</param>
    /// <param name="implementationAttributes">The exact raw MethodImplAttributes bits.</param>
    /// <param name="attributes">The exact raw MethodAttributes bits.</param>
    /// <param name="name">
    /// The exact decoded bounded MethodDef name; empty is retained without inferring the original heap index.
    /// </param>
    /// <param name="signaturePrefixBytes">
    /// An initialized prefix containing at most the draft grammar cap plus one bytes. When the exact total is known,
    /// the prefix must contain either every byte or exactly cap plus one bytes.
    /// </param>
    /// <param name="signatureByteCount">
    /// The exact complete signature length, or null when acquisition stopped before observing the source end.
    /// </param>
    /// <param name="parameterListRowId">
    /// The raw ParamList start. Only its physical simple-index width is checked in this draft because Param and
    /// ParamPtr source ends are not yet available.
    /// </param>
    /// <returns>A sealed immutable physical draft row with no caller-authored declaring type.</returns>
    public static MetadataMethodDefinitionRowObservationIdentity Create(
        StaticFieldMetadataModuleIdentity metadataModule,
        int methodDefinitionToken,
        int relativeVirtualAddress,
        int implementationAttributes,
        int attributes,
        string name,
        ImmutableArray<byte> signaturePrefixBytes,
        int? signatureByteCount,
        int parameterListRowId)
    {
        ArgumentNullException.ThrowIfNull(metadataModule);
        CanonicalReplayEncoding.ValidateMetadataToken(
            methodDefinitionToken,
            0x06,
            nameof(methodDefinitionToken));
        ExpressionV2ContractEncoding.RequireText(
            name,
            nameof(name),
            StaticFieldMetadataTextLimits.MaximumTextLength,
            allowEmpty: true);
        if (implementationAttributes is < 0 or > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(implementationAttributes),
                "The raw MethodImplAttributes value must fit its physical unsigned 16-bit column.");
        }
        if (attributes is < 0 or > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(attributes),
                "The raw MethodAttributes value must fit its physical unsigned 16-bit column.");
        }
        if (signaturePrefixBytes.IsDefault || signaturePrefixBytes.Length > MaximumSignatureByteCount + 1)
        {
            throw new ArgumentException(
                $"An initialized MethodDef signature prefix of at most {MaximumSignatureByteCount + 1} bytes is required.",
                nameof(signaturePrefixBytes));
        }
        if (signatureByteCount is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(signatureByteCount));
        }
        if (signatureByteCount is { } exactSignatureByteCount &&
            signaturePrefixBytes.Length != Math.Min(exactSignatureByteCount, MaximumSignatureByteCount + 1))
        {
            throw new ArgumentException(
                "The bounded MethodDef signature prefix must agree with its exact complete byte count.",
                nameof(signaturePrefixBytes));
        }
        if (parameterListRowId < 0 || parameterListRowId > MaximumListStartRowId)
        {
            throw new ArgumentOutOfRangeException(
                nameof(parameterListRowId),
                "The raw ParamList start must fit the physical metadata simple-index domain.");
        }

        return new MetadataMethodDefinitionRowObservationIdentity(
            metadataModule,
            methodDefinitionToken,
            relativeVirtualAddress,
            implementationAttributes,
            attributes,
            name,
            ExpressionV2ContractEncoding.Copy(signaturePrefixBytes),
            signatureByteCount,
            parameterListRowId);
    }

    /// <summary>Tests canonical equality between two physical MethodDef draft observations.</summary>
    /// <param name="other">The other draft observation.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(MetadataMethodDefinitionRowObservationIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests draft canonical equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for an observation with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataMethodDefinitionRowObservationIdentity);

    /// <summary>Computes a hash code from the immutable physical MethodDef draft bytes.</summary>
    /// <returns>A deterministic hash code for canonical draft content.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Freezes one MethodDef generic declaration after exact ownership and shared-grammar draft validation.</summary>
/// <remarks>
/// This sealed draft identity can be issued only by the complete catalog. Its declaring TypeDef token is derived from
/// exact direct or MethodPtr ownership, and its signature facts come only from the shared Core grammar certificate.
/// </remarks>
public sealed class MetadataMethodDefinitionTableRowIdentity :
    IEquatable<MetadataMethodDefinitionTableRowIdentity>
{
    private const string CanonicalDomain = "metadata-v2-methoddef-table-row";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataMethodDefinitionTableRowIdentity(
        MetadataSourceEndIdentity sourceEnds,
        MetadataMethodDefinitionRowObservationIdentity observation,
        int declaringTypeDefinitionToken,
        BoundedEcmaSignatureCertificate certificate)
    {
        SourceEnds = sourceEnds;
        Observation = observation;
        DeclaringTypeDefinitionToken = declaringTypeDefinitionToken;
        SignatureHeader = certificate.Header;
        CallingConvention = certificate.CallingConvention;
        HasThis = certificate.HasThis;
        HasExplicitThis = certificate.HasExplicitThis;
        DeclaredGenericArity = certificate.GenericParameterCount;
        ParameterCount = certificate.ParameterCount;
        AggregateTypeCount = certificate.Counters.AggregateTypeCount;
        AggregateGenericArgumentCount = certificate.Counters.AggregateGenericArgumentCount;
        MaximumSignatureDepth = certificate.Counters.MaximumObservedDepth;
        ProjectedSignatureNodeCount = certificate.Counters.ProjectedNodeCount;
        MaximumDeclaredGenericParameterCount = certificate.Counters.MaximumDeclaredGenericParameterCount;
        MaximumDeclaredParameterCount = certificate.Counters.MaximumDeclaredParameterCount;
        MaximumDeclaredArrayRank = certificate.Counters.MaximumDeclaredArrayRank;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteLengthPrefixedBytes(sourceEnds.CanonicalBytes.AsSpan());
        writer.WriteLengthPrefixedBytes(observation.CanonicalBytes.AsSpan());
        writer.WriteInt32(declaringTypeDefinitionToken);
        writer.WriteInt32(SignatureHeader);
        writer.WriteInt32(CallingConvention);
        writer.WriteBoolean(HasThis);
        writer.WriteBoolean(HasExplicitThis);
        writer.WriteInt32(DeclaredGenericArity);
        writer.WriteInt32(ParameterCount);
        writer.WriteInt32(AggregateTypeCount);
        writer.WriteInt32(AggregateGenericArgumentCount);
        writer.WriteInt32(MaximumSignatureDepth);
        writer.WriteInt32(ProjectedSignatureNodeCount);
        writer.WriteInt32(MaximumDeclaredGenericParameterCount);
        writer.WriteInt32(MaximumDeclaredParameterCount);
        writer.WriteInt32(MaximumDeclaredArrayRank);
        writer.WriteBoolean(true);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact source-end draft identity governing the physical row and declaring type.</summary>
    public MetadataSourceEndIdentity SourceEnds { get; }

    /// <summary>Gets the unchanged physical MethodDef draft observation.</summary>
    public MetadataMethodDefinitionRowObservationIdentity Observation { get; }

    /// <summary>Gets the exact physical draft MethodDef token.</summary>
    public int MethodDefinitionToken => Observation.MethodDefinitionToken;

    /// <summary>Gets the declaring TypeDef token derived from exact pointer-aware draft ownership.</summary>
    public int DeclaringTypeDefinitionToken { get; }

    /// <summary>Gets a defensive copy of the complete exact draft MethodDef signature.</summary>
    public ImmutableArray<byte> SignatureBytes => Observation.SignaturePrefixBytes;

    /// <summary>Gets whether the exact physical draft MethodDef is static.</summary>
    public bool IsStatic => Observation.IsStatic;

    /// <summary>Gets whether the exact physical draft MethodDef is an instance declaration.</summary>
    public bool IsInstance => !IsStatic;

    /// <summary>Gets whether the exact physical draft MethodDef declares generic parameters.</summary>
    public bool IsGeneric => DeclaredGenericArity > 0;

    /// <summary>Gets the complete shared-grammar draft signature header.</summary>
    public int SignatureHeader { get; }

    /// <summary>Gets the shared-grammar draft calling-convention discriminator.</summary>
    public int CallingConvention { get; }

    /// <summary>Gets the exact shared-grammar draft implicit-receiver flag.</summary>
    public bool HasThis { get; }

    /// <summary>Gets the exact shared-grammar draft explicit-receiver flag.</summary>
    /// <remarks>The current MethodDef grammar rejects this flag, so every issued exact draft row reports false.</remarks>
    public bool HasExplicitThis { get; }

    /// <summary>Gets the exact physical generic arity declared by the MethodDef signature draft.</summary>
    public int DeclaredGenericArity { get; }

    /// <summary>Gets the exact top-level parameter count declared by the MethodDef signature draft.</summary>
    public int ParameterCount { get; }

    /// <summary>Gets the shared-grammar aggregate draft type-node count.</summary>
    public int AggregateTypeCount { get; }

    /// <summary>Gets the shared-grammar aggregate draft generic-argument count.</summary>
    public int AggregateGenericArgumentCount { get; }

    /// <summary>Gets the greatest shared-grammar recursive draft type depth.</summary>
    public int MaximumSignatureDepth { get; }

    /// <summary>Gets the complete shared-grammar projected draft event-node count.</summary>
    public int ProjectedSignatureNodeCount { get; }

    /// <summary>Gets the greatest generic-parameter count declared by the method or a nested function-pointer draft.</summary>
    public int MaximumDeclaredGenericParameterCount { get; }

    /// <summary>Gets the greatest parameter count declared by the method or a nested function-pointer draft.</summary>
    public int MaximumDeclaredParameterCount { get; }

    /// <summary>Gets the greatest multidimensional-array rank declared anywhere in the signature draft.</summary>
    public int MaximumDeclaredArrayRank { get; }

    /// <summary>Gets the explicit full-signature source-end observation retained by this exact draft row.</summary>
    public bool SignatureSourceEndObserved => Observation.SignatureSourceEndObserved;

    /// <summary>Gets a defensive copy of the versioned canonical draft row bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft row.</summary>
    public string Sha256 { get; }

    internal static MetadataMethodDefinitionTableRowIdentity Create(
        object mintCapability,
        MetadataSourceEndIdentity sourceEnds,
        MetadataMethodDefinitionRowObservationIdentity observation,
        int declaringTypeDefinitionToken,
        BoundedEcmaSignatureCertificate certificate)
    {
        if (!MetadataMethodDefinitionTableCatalogIdentity.OwnsRowMintCapability(mintCapability))
        {
            throw new ArgumentException(
                "A derived MethodDef row requires the creating catalog's private mint capability.",
                nameof(mintCapability));
        }
        ArgumentNullException.ThrowIfNull(sourceEnds);
        ArgumentNullException.ThrowIfNull(observation);
        CanonicalReplayEncoding.ValidateMetadataToken(
            declaringTypeDefinitionToken,
            0x02,
            nameof(declaringTypeDefinitionToken));
        if (certificate.Form != BoundedEcmaSignatureForm.MethodDefinition ||
            !observation.SignatureSourceEndObserved ||
            certificate.SignatureByteCount != observation.SignaturePrefixBytesCore.Length ||
            observation.SignatureByteCount != certificate.SignatureByteCount)
        {
            throw new ArgumentException("A matching exact shared-grammar MethodDef draft certificate is required.");
        }
        return new MetadataMethodDefinitionTableRowIdentity(
            sourceEnds,
            observation,
            declaringTypeDefinitionToken,
            certificate);
    }

    /// <summary>Tests canonical equality between two complete MethodDef declaration draft rows.</summary>
    /// <param name="other">The other draft row.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(MetadataMethodDefinitionTableRowIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests complete MethodDef declaration draft equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a row with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataMethodDefinitionTableRowIdentity);

    /// <summary>Computes a hash code from the immutable complete MethodDef declaration draft bytes.</summary>
    /// <returns>A deterministic hash code for canonical draft content.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Freezes a complete pointer-aware MethodDef generic-declaration table draft projection.</summary>
/// <remarks>
/// This sealed draft catalog requires an exact TypeDef ownership catalog and exact RID-complete MethodDef rows. It
/// validates every signature with shared Core grammar before issuing any row, so every stop remains prefix-free.
/// </remarks>
public sealed class MetadataMethodDefinitionTableCatalogIdentity :
    IEquatable<MetadataMethodDefinitionTableCatalogIdentity>
{
    private const string CanonicalDomain = "metadata-v2-methoddef-table-catalog";
    private const int CanonicalSchemaVersion = 1;
    private static readonly object RowMintCapability = new();
    private static readonly BoundedEcmaSignatureLimits SignatureLimits = new(
        MetadataMethodDefinitionRowObservationIdentity.MaximumSignatureByteCount,
        StaticFieldV2Limits.MaximumTypeSpecificationDepth,
        StaticFieldV2Limits.MaximumRawTypeSignatureNodeCount,
        StaticFieldV2Limits.MaximumTypeSpecificationArgumentCount,
        int.MaxValue,
        StaticFieldV2Limits.MaximumParameterCount,
        StaticFieldV2Limits.MaximumLocalCount,
        StaticFieldV2Limits.MaximumArrayRank);

    private readonly ImmutableArray<MetadataMethodDefinitionTableRowIdentity> rows;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataMethodDefinitionTableCatalogIdentity(
        MetadataMethodDefinitionTableResultKind resultKind,
        MetadataMethodDefinitionTableIssue issue,
        MetadataTypeDefinitionTableCatalogIdentity typeDefinitionCatalog,
        ImmutableArray<MetadataMethodDefinitionTableRowIdentity> rows,
        EvaluationDeterministicBound? reachedBound,
        int observedCount,
        int? stoppedMethodDefinitionToken)
    {
        ResultKind = resultKind;
        Issue = issue;
        TypeDefinitionCatalog = typeDefinitionCatalog;
        this.rows = rows;
        ReachedBound = reachedBound;
        ObservedCount = observedCount;
        StoppedMethodDefinitionToken = stoppedMethodDefinitionToken;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32((int)resultKind);
        writer.WriteInt32((int)issue);
        writer.WriteLengthPrefixedBytes(typeDefinitionCatalog.CanonicalBytes.AsSpan());
        ExpressionV2ContractEncoding.WriteCanonicalArray(writer, rows, static row => row.CanonicalBytes);
        writer.WriteBoolean(reachedBound is not null);
        if (reachedBound is not null)
        {
            writer.WriteString(reachedBound.Name);
            writer.WriteInt64(reachedBound.Value);
        }
        writer.WriteInt32(observedCount);
        ExpressionV2ContractEncoding.WriteOptionalInt32(writer, stoppedMethodDefinitionToken);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets whether the complete draft MethodDef table is exact, non-exact, or invalid.</summary>
    public MetadataMethodDefinitionTableResultKind ResultKind { get; }

    /// <summary>Gets the typed draft issue, or none for an exact result.</summary>
    public MetadataMethodDefinitionTableIssue Issue { get; }

    /// <summary>Gets the pointer-aware TypeDef draft catalog supplying every declaring owner.</summary>
    public MetadataTypeDefinitionTableCatalogIdentity TypeDefinitionCatalog { get; }

    /// <summary>Gets the exact source ends inherited from the pointer-aware TypeDef draft catalog.</summary>
    public MetadataSourceEndIdentity SourceEnds => TypeDefinitionCatalog.SourceEnds;

    /// <summary>Gets a defensive RID-order copy of exact derived MethodDef draft rows, or an empty array otherwise.</summary>
    public ImmutableArray<MetadataMethodDefinitionTableRowIdentity> Rows =>
        ExpressionV2ContractEncoding.Copy(rows);

    /// <summary>Gets the exact reached draft bound only for a cap-plus-one result.</summary>
    public EvaluationDeterministicBound? ReachedBound { get; }

    /// <summary>Gets the issue-related draft supplied count or cap-plus-one observation.</summary>
    public int ObservedCount { get; }

    /// <summary>Gets the physical MethodDef token at which signature or row validation stopped, otherwise null.</summary>
    public int? StoppedMethodDefinitionToken { get; }

    /// <summary>Gets a defensive copy of the versioned canonical draft catalog bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft catalog.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one complete pointer-aware MethodDef generic-declaration table draft projection.</summary>
    /// <param name="typeDefinitionCatalog">The complete pointer-aware TypeDef ownership draft catalog.</param>
    /// <param name="observations">
    /// Every physical MethodDef row in RID order; default denotes unavailable acquisition when the source is non-empty.
    /// </param>
    /// <returns>An exact catalog, a prefix-free non-exact stop, or a factless invalid draft result.</returns>
    public static MetadataMethodDefinitionTableCatalogIdentity Create(
        MetadataTypeDefinitionTableCatalogIdentity typeDefinitionCatalog,
        ImmutableArray<MetadataMethodDefinitionRowObservationIdentity> observations)
    {
        ArgumentNullException.ThrowIfNull(typeDefinitionCatalog);
        if (typeDefinitionCatalog.ResultKind == MetadataTypeDefinitionTableResultKind.NonExact)
        {
            return NonExact(
                typeDefinitionCatalog,
                MetadataMethodDefinitionTableIssue.TypeDefinitionCatalogNonExact,
                typeDefinitionCatalog.ReachedBound,
                typeDefinitionCatalog.ObservedCount,
                null);
        }
        if (typeDefinitionCatalog.ResultKind == MetadataTypeDefinitionTableResultKind.Invalid)
        {
            return Invalid(
                typeDefinitionCatalog,
                MapOwnershipDependencyIssue(typeDefinitionCatalog),
                typeDefinitionCatalog.ObservedCount,
                null);
        }

        var sourceCount = typeDefinitionCatalog.SourceEnds.MethodDefinitionRowCount;
        if (sourceCount > StaticFieldV2Limits.MaximumMethodDefinitionRowCount)
        {
            return NonExact(
                typeDefinitionCatalog,
                MetadataMethodDefinitionTableIssue.TypeDefinitionCatalogNonExact,
                new EvaluationDeterministicBound(
                    ExpressionV2ContractLimits.MethodDefinitionRowCountBoundName,
                    StaticFieldV2Limits.MaximumMethodDefinitionRowCount),
                StaticFieldV2Limits.MaximumMethodDefinitionRowCount + 1,
                null);
        }
        if (observations.IsDefault && sourceCount == 0)
        {
            observations = ImmutableArray<MetadataMethodDefinitionRowObservationIdentity>.Empty;
        }
        if (observations.IsDefault || observations.Length < sourceCount)
        {
            return NonExact(
                typeDefinitionCatalog,
                MetadataMethodDefinitionTableIssue.TableIncomplete,
                null,
                observations.IsDefault ? 0 : observations.Length,
                null);
        }
        if (observations.Length > sourceCount)
        {
            return Invalid(
                typeDefinitionCatalog,
                MetadataMethodDefinitionTableIssue.TableRowCountConflict,
                observations.Length,
                null);
        }

        var copied = observations.IsEmpty
            ? ImmutableArray<MetadataMethodDefinitionRowObservationIdentity>.Empty
            : ImmutableArray.CreateRange(observations);
        for (var index = 0; index < copied.Length; index++)
        {
            var observation = copied[index];
            if (observation is null ||
                observation.MethodDefinitionToken != (0x0600_0000 | checked(index + 1)))
            {
                return Invalid(
                    typeDefinitionCatalog,
                    MetadataMethodDefinitionTableIssue.PhysicalOrderInvalid,
                    copied.Length,
                    observation?.MethodDefinitionToken);
            }
            if (!observation.MetadataModule.Equals(typeDefinitionCatalog.SourceEnds.SourceModule))
            {
                return Invalid(
                    typeDefinitionCatalog,
                    MetadataMethodDefinitionTableIssue.SourceModuleMismatch,
                    copied.Length,
                    observation.MethodDefinitionToken);
            }
        }

        var ownershipIssue = BuildOwnershipMap(
            typeDefinitionCatalog,
            sourceCount,
            out var declaringTypeTokens,
            out var ownershipStoppedMethodToken);
        if (ownershipIssue != MetadataMethodDefinitionTableIssue.None)
        {
            return Invalid(
                typeDefinitionCatalog,
                ownershipIssue,
                sourceCount,
                ownershipStoppedMethodToken);
        }

        var certificates = new BoundedEcmaSignatureCertificate[copied.Length];
        for (var index = 0; index < copied.Length; index++)
        {
            var observation = copied[index];
            if (observation.SignaturePrefixBytesCore.Length >
                MetadataMethodDefinitionRowObservationIdentity.MaximumSignatureByteCount)
            {
                return NonExact(
                    typeDefinitionCatalog,
                    MetadataMethodDefinitionTableIssue.SignatureByteBoundReached,
                    new EvaluationDeterministicBound(
                        MetadataMethodDefinitionRowObservationIdentity.MaximumSignatureByteCountBoundName,
                        MetadataMethodDefinitionRowObservationIdentity.MaximumSignatureByteCount),
                    MetadataMethodDefinitionRowObservationIdentity.MaximumSignatureByteCount + 1,
                    observation.MethodDefinitionToken);
            }
            if (!observation.SignatureSourceEndObserved)
            {
                return NonExact(
                    typeDefinitionCatalog,
                    MetadataMethodDefinitionTableIssue.SignatureIncomplete,
                    null,
                    observation.SignaturePrefixBytesCore.Length,
                    observation.MethodDefinitionToken);
            }

            var decode = BoundedEcmaSignatureProjection.Decode(
                observation.SignaturePrefixBytesCore.AsSpan(),
                BoundedEcmaSignatureForm.MethodDefinition,
                SignatureLimits);
            if (decode.Kind == BoundedEcmaSignatureDecodeKind.BoundReached)
            {
                var mapped = MapSignatureBound(decode.ReachedBound, decode.Counters);
                return NonExact(
                    typeDefinitionCatalog,
                    mapped.Issue,
                    mapped.Bound,
                    mapped.ObservedCount,
                    observation.MethodDefinitionToken);
            }
            if (decode.Kind == BoundedEcmaSignatureDecodeKind.Invalid || decode.Certificate is not { } certificate)
            {
                return Invalid(
                    typeDefinitionCatalog,
                    decode.Failure == BoundedEcmaSignatureFailureKind.TrailingData
                        ? MetadataMethodDefinitionTableIssue.SignatureTrailingData
                        : MetadataMethodDefinitionTableIssue.SignatureInvalid,
                    observation.SignaturePrefixBytesCore.Length,
                    observation.MethodDefinitionToken);
            }
            if (certificate.HasThis == observation.IsStatic)
            {
                return Invalid(
                    typeDefinitionCatalog,
                    MetadataMethodDefinitionTableIssue.ReceiverMismatch,
                    0,
                    observation.MethodDefinitionToken);
            }
            certificates[index] = certificate;
        }

        var rows = ImmutableArray.CreateBuilder<MetadataMethodDefinitionTableRowIdentity>(copied.Length);
        for (var index = 0; index < copied.Length; index++)
        {
            rows.Add(MetadataMethodDefinitionTableRowIdentity.Create(
                RowMintCapability,
                typeDefinitionCatalog.SourceEnds,
                copied[index],
                declaringTypeTokens[index],
                certificates[index]));
        }
        return new MetadataMethodDefinitionTableCatalogIdentity(
            MetadataMethodDefinitionTableResultKind.Exact,
            MetadataMethodDefinitionTableIssue.None,
            typeDefinitionCatalog,
            rows.MoveToImmutable(),
            null,
            0,
            null);
    }

    /// <summary>Tests canonical equality between two complete MethodDef table draft projections.</summary>
    /// <param name="other">The other draft catalog.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(MetadataMethodDefinitionTableCatalogIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests complete MethodDef table draft equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a catalog with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataMethodDefinitionTableCatalogIdentity);

    /// <summary>Computes a hash code from the immutable complete MethodDef table draft bytes.</summary>
    /// <returns>A deterministic hash code for canonical draft content.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static bool OwnsRowMintCapability(object? capability) =>
        ReferenceEquals(capability, RowMintCapability);

    private static MetadataMethodDefinitionTableIssue BuildOwnershipMap(
        MetadataTypeDefinitionTableCatalogIdentity typeDefinitionCatalog,
        int methodCount,
        out int[] declaringTypeTokens,
        out int? stoppedMethodToken)
    {
        declaringTypeTokens = new int[methodCount];
        stoppedMethodToken = null;
        foreach (var typeRow in typeDefinitionCatalog.Rows)
        {
            foreach (var methodToken in typeRow.MethodDefinitionTokens)
            {
                if (!CanonicalReplayEncoding.IsMetadataTokenForTable(methodToken, 0x06))
                {
                    stoppedMethodToken = methodToken;
                    return MetadataMethodDefinitionTableIssue.OwnershipTargetOutOfRange;
                }
                var rowId = CanonicalReplayEncoding.MetadataTokenRowId(methodToken);
                if (rowId <= 0 || rowId > methodCount)
                {
                    stoppedMethodToken = methodToken;
                    return MetadataMethodDefinitionTableIssue.OwnershipTargetOutOfRange;
                }
                if (declaringTypeTokens[rowId - 1] != 0)
                {
                    stoppedMethodToken = methodToken;
                    return MetadataMethodDefinitionTableIssue.OwnershipDuplicate;
                }
                declaringTypeTokens[rowId - 1] = typeRow.TypeDefinitionToken;
            }
        }
        for (var index = 0; index < declaringTypeTokens.Length; index++)
        {
            if (declaringTypeTokens[index] == 0)
            {
                stoppedMethodToken = 0x0600_0000 | checked(index + 1);
                return MetadataMethodDefinitionTableIssue.OwnershipMissing;
            }
        }
        return MetadataMethodDefinitionTableIssue.None;
    }

    private static MetadataMethodDefinitionTableIssue MapOwnershipDependencyIssue(
        MetadataTypeDefinitionTableCatalogIdentity typeDefinitionCatalog)
    {
        if (typeDefinitionCatalog.Issue == MetadataTypeDefinitionTableIssue.MethodListCoverageInvalid)
        {
            return MetadataMethodDefinitionTableIssue.OwnershipMissing;
        }
        if (typeDefinitionCatalog.MemberPointerCatalog.Issue ==
            MetadataMemberPointerTableIssue.MethodPointerTargetDuplicate)
        {
            return MetadataMethodDefinitionTableIssue.OwnershipDuplicate;
        }
        if (typeDefinitionCatalog.MemberPointerCatalog.Issue is
            MetadataMemberPointerTableIssue.MethodPointerTargetOutOfRange or
            MetadataMemberPointerTableIssue.MethodPointerTargetTableInvalid)
        {
            return MetadataMethodDefinitionTableIssue.OwnershipTargetOutOfRange;
        }
        return MetadataMethodDefinitionTableIssue.TypeDefinitionCatalogInvalid;
    }

    private static SignatureBound MapSignatureBound(
        BoundedEcmaSignatureBoundKind boundKind,
        BoundedEcmaSignatureCounters counters)
    {
        var (issue, name, limit, observed) = boundKind switch
        {
            BoundedEcmaSignatureBoundKind.ByteCount => (
                MetadataMethodDefinitionTableIssue.SignatureByteBoundReached,
                MetadataMethodDefinitionRowObservationIdentity.MaximumSignatureByteCountBoundName,
                MetadataMethodDefinitionRowObservationIdentity.MaximumSignatureByteCount,
                counters.InputByteCount),
            BoundedEcmaSignatureBoundKind.RecursiveDepth => (
                MetadataMethodDefinitionTableIssue.SignatureDepthBoundReached,
                ExpressionV2ContractLimits.TypeSpecificationDepthBoundName,
                StaticFieldV2Limits.MaximumTypeSpecificationDepth,
                counters.MaximumObservedDepth),
            BoundedEcmaSignatureBoundKind.AggregateGenericArgumentCount => (
                MetadataMethodDefinitionTableIssue.SignatureGenericArgumentBoundReached,
                ExpressionV2ContractLimits.TypeSpecificationArgumentCountBoundName,
                StaticFieldV2Limits.MaximumTypeSpecificationArgumentCount,
                counters.AggregateGenericArgumentCount),
            BoundedEcmaSignatureBoundKind.ParameterCount => (
                MetadataMethodDefinitionTableIssue.SignatureParameterBoundReached,
                ExpressionV2ContractLimits.ParameterCountBoundName,
                StaticFieldV2Limits.MaximumParameterCount,
                counters.MaximumDeclaredParameterCount),
            BoundedEcmaSignatureBoundKind.ArrayRank => (
                MetadataMethodDefinitionTableIssue.SignatureArrayRankBoundReached,
                ExpressionV2ContractLimits.ArrayRankBoundName,
                StaticFieldV2Limits.MaximumArrayRank,
                counters.MaximumDeclaredArrayRank),
            _ => throw new InvalidOperationException("The shared grammar reported an inapplicable MethodDef bound."),
        };
        return new SignatureBound(
            issue,
            new EvaluationDeterministicBound(name, limit),
            Math.Min(Math.Max(observed, limit + 1), limit + 1));
    }

    private readonly record struct SignatureBound(
        MetadataMethodDefinitionTableIssue Issue,
        EvaluationDeterministicBound Bound,
        int ObservedCount);

    private static MetadataMethodDefinitionTableCatalogIdentity NonExact(
        MetadataTypeDefinitionTableCatalogIdentity typeDefinitionCatalog,
        MetadataMethodDefinitionTableIssue issue,
        EvaluationDeterministicBound? reachedBound,
        int observedCount,
        int? stoppedMethodDefinitionToken) =>
        new(
            MetadataMethodDefinitionTableResultKind.NonExact,
            issue,
            typeDefinitionCatalog,
            ImmutableArray<MetadataMethodDefinitionTableRowIdentity>.Empty,
            reachedBound,
            observedCount,
            stoppedMethodDefinitionToken);

    private static MetadataMethodDefinitionTableCatalogIdentity Invalid(
        MetadataTypeDefinitionTableCatalogIdentity typeDefinitionCatalog,
        MetadataMethodDefinitionTableIssue issue,
        int observedCount,
        int? stoppedMethodDefinitionToken) =>
        new(
            MetadataMethodDefinitionTableResultKind.Invalid,
            issue,
            typeDefinitionCatalog,
            ImmutableArray<MetadataMethodDefinitionTableRowIdentity>.Empty,
            null,
            observedCount,
            stoppedMethodDefinitionToken);
}
