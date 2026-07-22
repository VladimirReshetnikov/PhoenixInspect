using System.Collections.Immutable;
using Interpreter.Core.Abstractions;

namespace Interpreter.Product.DumpQuery;

/// <summary>Freezes one source-anchored physical AssemblyRef-row draft observation.</summary>
/// <remarks>The W8 observation owns raw row payload and makes no target AssemblyDef assertion.</remarks>
public sealed class MetadataAssemblyReferenceRowObservationIdentity :
    IEquatable<MetadataAssemblyReferenceRowObservationIdentity>
{
    private const string CanonicalDomain = "metadata-v2-assemblyref-row-observation";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> publicKeyOrToken;
    private readonly ImmutableArray<byte> hashValue;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataAssemblyReferenceRowObservationIdentity(
        StaticFieldMetadataModuleIdentity sourceModule,
        StaticFieldAssemblyReferenceIdentity validatedPayload)
    {
        SourceModule = sourceModule;
        AssemblyReferenceToken = validatedPayload.AssemblyReferenceToken;
        Name = validatedPayload.Name;
        MajorVersion = validatedPayload.MajorVersion;
        MinorVersion = validatedPayload.MinorVersion;
        BuildNumber = validatedPayload.BuildNumber;
        RevisionNumber = validatedPayload.RevisionNumber;
        Culture = validatedPayload.Culture;
        Flags = validatedPayload.Flags;
        publicKeyOrToken = validatedPayload.PublicKeyOrToken;
        hashValue = validatedPayload.HashValue;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteSha256(sourceModule.Sha256, nameof(sourceModule));
        writer.WriteInt32(AssemblyReferenceToken);
        writer.WriteString(Name);
        writer.WriteInt32(MajorVersion);
        writer.WriteInt32(MinorVersion);
        writer.WriteInt32(BuildNumber);
        writer.WriteInt32(RevisionNumber);
        writer.WriteString(Culture);
        writer.WriteInt32(Flags);
        writer.WriteLengthPrefixedBytes(publicKeyOrToken.AsSpan());
        writer.WriteLengthPrefixedBytes(hashValue.AsSpan());
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact metadata module containing the physical AssemblyRef row.</summary>
    public StaticFieldMetadataModuleIdentity SourceModule { get; }

    /// <summary>Gets the exact non-nil physical AssemblyRef token.</summary>
    public int AssemblyReferenceToken { get; }

    /// <summary>Gets the exact non-empty assembly name.</summary>
    public string Name { get; }

    /// <summary>Gets the unsigned 16-bit major version as an integer.</summary>
    public int MajorVersion { get; }

    /// <summary>Gets the unsigned 16-bit minor version as an integer.</summary>
    public int MinorVersion { get; }

    /// <summary>Gets the unsigned 16-bit build number as an integer.</summary>
    public int BuildNumber { get; }

    /// <summary>Gets the unsigned 16-bit revision number as an integer.</summary>
    public int RevisionNumber { get; }

    /// <summary>Gets the exact culture string, including empty for neutral culture.</summary>
    public string Culture { get; }

    /// <summary>Gets the exact raw AssemblyRef flags.</summary>
    public int Flags { get; }

    /// <summary>Gets a defensive copy of the physical public-key-or-token blob.</summary>
    public ImmutableArray<byte> PublicKeyOrToken => ExpressionV2ContractEncoding.Copy(publicKeyOrToken);

    /// <summary>Gets a defensive copy of the physical AssemblyRef hash-value blob.</summary>
    public ImmutableArray<byte> HashValue => ExpressionV2ContractEncoding.Copy(hashValue);

    /// <summary>Gets a defensive copy of the canonical physical draft observation bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft observation bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one source-anchored physical AssemblyRef-row draft observation.</summary>
    /// <param name="sourceModule">The exact metadata module containing the physical row.</param>
    /// <param name="assemblyReferenceToken">The exact non-nil AssemblyRef token.</param>
    /// <param name="name">The exact non-empty assembly name.</param>
    /// <param name="majorVersion">The unsigned 16-bit major version.</param>
    /// <param name="minorVersion">The unsigned 16-bit minor version.</param>
    /// <param name="buildNumber">The unsigned 16-bit build number.</param>
    /// <param name="revisionNumber">The unsigned 16-bit revision number.</param>
    /// <param name="culture">The exact culture string, including empty.</param>
    /// <param name="flags">The exact raw AssemblyRef flags.</param>
    /// <param name="publicKeyOrToken">The initialized physical key-or-token blob.</param>
    /// <param name="hashValue">The initialized physical hash-value blob.</param>
    /// <returns>A physical W8 draft observation with no target-assembly assertion.</returns>
    public static MetadataAssemblyReferenceRowObservationIdentity Create(
        StaticFieldMetadataModuleIdentity sourceModule,
        int assemblyReferenceToken,
        string name,
        int majorVersion,
        int minorVersion,
        int buildNumber,
        int revisionNumber,
        string culture,
        int flags,
        ImmutableArray<byte> publicKeyOrToken,
        ImmutableArray<byte> hashValue)
    {
        ArgumentNullException.ThrowIfNull(sourceModule);
        var payload = StaticFieldAssemblyReferenceIdentity.Create(
            assemblyReferenceToken,
            name,
            majorVersion,
            minorVersion,
            buildNumber,
            revisionNumber,
            culture,
            flags,
            publicKeyOrToken,
            hashValue);
        return new MetadataAssemblyReferenceRowObservationIdentity(sourceModule, payload);
    }

    /// <summary>Tests whether this physical draft observation has the same payload as one W7 candidate row.</summary>
    /// <param name="candidate">The W7 AssemblyRef candidate to compare.</param>
    /// <returns><see langword="true"/> only when every physical row field agrees.</returns>
    public bool MatchesCandidate(StaticFieldAssemblyReferenceIdentity candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        return AssemblyReferenceToken == candidate.AssemblyReferenceToken &&
            StringComparer.Ordinal.Equals(Name, candidate.Name) &&
            MajorVersion == candidate.MajorVersion && MinorVersion == candidate.MinorVersion &&
            BuildNumber == candidate.BuildNumber && RevisionNumber == candidate.RevisionNumber &&
            StringComparer.Ordinal.Equals(Culture, candidate.Culture) && Flags == candidate.Flags &&
            publicKeyOrToken.AsSpan().SequenceEqual(candidate.PublicKeyOrToken.AsSpan()) &&
            hashValue.AsSpan().SequenceEqual(candidate.HashValue.AsSpan());
    }

    /// <summary>Tests canonical equality between two physical AssemblyRef draft observations.</summary>
    /// <param name="other">The other draft observation.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(MetadataAssemblyReferenceRowObservationIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests physical AssemblyRef draft observation equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for an observation with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataAssemblyReferenceRowObservationIdentity);

    /// <summary>Computes a deterministic hash code from canonical physical draft content.</summary>
    /// <returns>A hash code for this physical AssemblyRef draft observation.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Freezes one guarded physical AssemblyRef draft row.</summary>
/// <remarks>Only an exact complete AssemblyRef table can mint this fixed-reference draft row.</remarks>
public sealed class MetadataAssemblyReferencePhysicalRowIdentity :
    IEquatable<MetadataAssemblyReferencePhysicalRowIdentity>
{
    private const string CanonicalDomain = "metadata-v2-assemblyref-physical-row";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataAssemblyReferencePhysicalRowIdentity(
        MetadataReferenceSourceEndIdentity sourceEnds,
        MetadataAssemblyReferenceRowObservationIdentity observation)
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

    /// <summary>Gets the unchanged physical AssemblyRef-row draft observation.</summary>
    public MetadataAssemblyReferenceRowObservationIdentity Observation { get; }

    /// <summary>Gets the exact non-nil physical AssemblyRef token.</summary>
    public int AssemblyReferenceToken => Observation.AssemblyReferenceToken;

    /// <summary>Gets a defensive copy of the fixed-reference canonical draft row bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft row bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Tests canonical equality between two guarded physical AssemblyRef draft rows.</summary>
    /// <param name="other">The other draft row.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(MetadataAssemblyReferencePhysicalRowIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests guarded physical AssemblyRef draft equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a row with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataAssemblyReferencePhysicalRowIdentity);

    /// <summary>Computes a deterministic hash code from fixed-reference canonical draft content.</summary>
    /// <returns>A hash code for this guarded physical AssemblyRef draft row.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static MetadataAssemblyReferencePhysicalRowIdentity Create(
        object mintCapability,
        MetadataReferenceSourceEndIdentity sourceEnds,
        MetadataAssemblyReferenceRowObservationIdentity observation)
    {
        if (!MetadataAssemblyReferencePhysicalTableCatalogIdentity.OwnsRowMintCapability(mintCapability))
        {
            throw new ArgumentException("A physical AssemblyRef row requires its complete table's private mint capability.");
        }
        return new MetadataAssemblyReferencePhysicalRowIdentity(sourceEnds, observation);
    }
}

/// <summary>Freezes one complete RID-ordered physical AssemblyRef draft table.</summary>
/// <remarks>Every catalog-level stop is prefix-free and retains no guarded row.</remarks>
public sealed class MetadataAssemblyReferencePhysicalTableCatalogIdentity :
    IEquatable<MetadataAssemblyReferencePhysicalTableCatalogIdentity>
{
    /// <summary>Gets the deterministic draft bound name for physical AssemblyRef rows.</summary>
    public const string TableRowCountBoundName = "metadata-v2.physical-assemblyref.rows";

    private const string CanonicalDomain = "metadata-v2-assemblyref-physical-table";
    private const int CanonicalSchemaVersion = 1;
    private static readonly object RowMintCapability = new();
    private readonly ImmutableArray<MetadataAssemblyReferencePhysicalRowIdentity> rows;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataAssemblyReferencePhysicalTableCatalogIdentity(
        MetadataReferencePhysicalTableResultKind resultKind,
        MetadataReferencePhysicalTableIssue issue,
        MetadataReferenceSourceEndIdentity sourceEnds,
        ImmutableArray<MetadataAssemblyReferencePhysicalRowIdentity> rows,
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

    /// <summary>Gets whether this physical AssemblyRef draft table is exact, non-exact, or invalid.</summary>
    public MetadataReferencePhysicalTableResultKind ResultKind { get; }

    /// <summary>Gets the typed complete physical AssemblyRef draft issue.</summary>
    public MetadataReferencePhysicalTableIssue Issue { get; }

    /// <summary>Gets the exact draft reference source ends for this table.</summary>
    public MetadataReferenceSourceEndIdentity SourceEnds { get; }

    /// <summary>Gets a defensive RID-order copy of exact guarded rows, or an empty vector for every stop.</summary>
    public ImmutableArray<MetadataAssemblyReferencePhysicalRowIdentity> Rows =>
        ExpressionV2ContractEncoding.Copy(rows);

    /// <summary>Gets the physical AssemblyRef row-count draft bound after a cap-plus-one outcome.</summary>
    public EvaluationDeterministicBound? ReachedBound { get; }

    /// <summary>Gets the supplied, exact, or cap-plus-one draft observation count.</summary>
    public int ObservedCount { get; }

    /// <summary>Gets a defensive copy of the fixed-reference canonical draft table bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft table bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one complete RID-ordered physical AssemblyRef draft table.</summary>
    /// <param name="sourceEnds">The exact reference source ends defining the physical table length.</param>
    /// <param name="observations">Every source-anchored physical AssemblyRef row in RID order.</param>
    /// <returns>An exact guarded table or a prefix-free typed draft stop.</returns>
    public static MetadataAssemblyReferencePhysicalTableCatalogIdentity Create(
        MetadataReferenceSourceEndIdentity sourceEnds,
        ImmutableArray<MetadataAssemblyReferenceRowObservationIdentity> observations)
    {
        ArgumentNullException.ThrowIfNull(sourceEnds);
        var issue = MetadataReferencePhysicalTableValidation.ValidateVector(
            sourceEnds.AssemblyReferenceRowCount,
            observations,
            0x23,
            static row => row.AssemblyReferenceToken,
            out var resultKind,
            out var reachedBound,
            out var observedCount,
            TableRowCountBoundName);
        if (issue != MetadataReferencePhysicalTableIssue.None)
        {
            return new MetadataAssemblyReferencePhysicalTableCatalogIdentity(
                resultKind, issue, sourceEnds, [], reachedBound, observedCount);
        }
        if (observations.Any(row => !row.SourceModule.Equals(sourceEnds.SourceModule)))
        {
            return new MetadataAssemblyReferencePhysicalTableCatalogIdentity(
                MetadataReferencePhysicalTableResultKind.Invalid,
                MetadataReferencePhysicalTableIssue.SourceMismatch,
                sourceEnds,
                [],
                null,
                observations.Length);
        }
        var builder = ImmutableArray.CreateBuilder<MetadataAssemblyReferencePhysicalRowIdentity>(observations.Length);
        foreach (var observation in observations)
        {
            builder.Add(MetadataAssemblyReferencePhysicalRowIdentity.Create(RowMintCapability, sourceEnds, observation));
        }
        return new MetadataAssemblyReferencePhysicalTableCatalogIdentity(
            MetadataReferencePhysicalTableResultKind.Exact,
            MetadataReferencePhysicalTableIssue.None,
            sourceEnds,
            builder.MoveToImmutable(),
            null,
            observations.Length);
    }

    /// <summary>Finds one exact guarded physical AssemblyRef draft row by token.</summary>
    /// <param name="assemblyReferenceToken">The non-nil physical AssemblyRef token.</param>
    /// <returns>The exact draft row, or null when the table or token is not exact.</returns>
    public MetadataAssemblyReferencePhysicalRowIdentity? FindRow(int assemblyReferenceToken)
    {
        if (ResultKind != MetadataReferencePhysicalTableResultKind.Exact ||
            !CanonicalReplayEncoding.IsMetadataTokenForTable(assemblyReferenceToken, 0x23))
        {
            return null;
        }
        var rowId = CanonicalReplayEncoding.MetadataTokenRowId(assemblyReferenceToken);
        return rowId > 0 && rowId <= rows.Length ? rows[rowId - 1] : null;
    }

    /// <summary>Tests canonical equality between two complete physical AssemblyRef draft tables.</summary>
    /// <param name="other">The other draft table.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(MetadataAssemblyReferencePhysicalTableCatalogIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests complete physical AssemblyRef draft-table equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a table with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataAssemblyReferencePhysicalTableCatalogIdentity);

    /// <summary>Computes a deterministic hash code from fixed-reference canonical draft content.</summary>
    /// <returns>A hash code for this complete physical AssemblyRef draft table.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static bool OwnsRowMintCapability(object? capability) => ReferenceEquals(capability, RowMintCapability);
}

/// <summary>Freezes one source-anchored physical TypeSpec-row draft observation.</summary>
/// <remarks>The W8 observation retains complete bytes independently of any caller-supplied decode graph.</remarks>
public sealed class MetadataTypeSpecificationRowObservationIdentity :
    IEquatable<MetadataTypeSpecificationRowObservationIdentity>
{
    private const string CanonicalDomain = "metadata-v2-typespec-row-observation-authority-input";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> signatureBytes;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataTypeSpecificationRowObservationIdentity(
        StaticFieldMetadataModuleIdentity sourceModule,
        int typeSpecificationToken,
        ImmutableArray<byte> signatureBytes)
    {
        SourceModule = sourceModule;
        TypeSpecificationToken = typeSpecificationToken;
        this.signatureBytes = ExpressionV2ContractEncoding.Copy(signatureBytes);
        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteSha256(sourceModule.Sha256, nameof(sourceModule));
        writer.WriteInt32(typeSpecificationToken);
        writer.WriteLengthPrefixedBytes(signatureBytes.AsSpan());
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact metadata module containing the physical TypeSpec row.</summary>
    public StaticFieldMetadataModuleIdentity SourceModule { get; }

    /// <summary>Gets the exact non-nil physical TypeSpec token.</summary>
    public int TypeSpecificationToken { get; }

    /// <summary>Gets a defensive copy of the complete original TypeSpec signature blob.</summary>
    public ImmutableArray<byte> SignatureBytes => ExpressionV2ContractEncoding.Copy(signatureBytes);

    /// <summary>Gets a defensive copy of the canonical physical draft observation bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft observation bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one source-anchored physical TypeSpec-row draft observation.</summary>
    /// <param name="sourceModule">The exact metadata module containing the physical row.</param>
    /// <param name="typeSpecificationToken">The exact non-nil TypeSpec token.</param>
    /// <param name="signatureBytes">The complete initialized physical signature blob.</param>
    /// <returns>A physical W8 draft observation independent of later decoding.</returns>
    public static MetadataTypeSpecificationRowObservationIdentity Create(
        StaticFieldMetadataModuleIdentity sourceModule,
        int typeSpecificationToken,
        ImmutableArray<byte> signatureBytes)
    {
        ArgumentNullException.ThrowIfNull(sourceModule);
        var reference = MetadataTypeSpecificationRowReferenceIdentity.Create(sourceModule, typeSpecificationToken);
        _ = MetadataTypeSpecificationRowIdentity.Create(reference, signatureBytes);
        return new MetadataTypeSpecificationRowObservationIdentity(
            sourceModule,
            typeSpecificationToken,
            signatureBytes);
    }

    /// <summary>Tests canonical equality between two physical TypeSpec draft observations.</summary>
    /// <param name="other">The other draft observation.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(MetadataTypeSpecificationRowObservationIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests physical TypeSpec draft observation equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for an observation with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataTypeSpecificationRowObservationIdentity);

    /// <summary>Computes a deterministic hash code from canonical physical draft content.</summary>
    /// <returns>A hash code for this physical TypeSpec draft observation.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Freezes one guarded physical TypeSpec draft row.</summary>
/// <remarks>Only an exact complete TypeSpec table can mint this fixed-reference draft row.</remarks>
public sealed class MetadataTypeSpecificationPhysicalRowIdentity :
    IEquatable<MetadataTypeSpecificationPhysicalRowIdentity>
{
    private const string CanonicalDomain = "metadata-v2-typespec-physical-row";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataTypeSpecificationPhysicalRowIdentity(
        MetadataReferenceSourceEndIdentity sourceEnds,
        MetadataTypeSpecificationRowObservationIdentity observation)
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

    /// <summary>Gets the unchanged physical TypeSpec-row draft observation.</summary>
    public MetadataTypeSpecificationRowObservationIdentity Observation { get; }

    /// <summary>Gets the exact non-nil physical TypeSpec token.</summary>
    public int TypeSpecificationToken => Observation.TypeSpecificationToken;

    /// <summary>Gets a defensive copy of the fixed-reference canonical draft row bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft row bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Tests canonical equality between two guarded physical TypeSpec draft rows.</summary>
    /// <param name="other">The other draft row.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(MetadataTypeSpecificationPhysicalRowIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests guarded physical TypeSpec draft equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a row with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataTypeSpecificationPhysicalRowIdentity);

    /// <summary>Computes a deterministic hash code from fixed-reference canonical draft content.</summary>
    /// <returns>A hash code for this guarded physical TypeSpec draft row.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static MetadataTypeSpecificationPhysicalRowIdentity Create(
        object mintCapability,
        MetadataReferenceSourceEndIdentity sourceEnds,
        MetadataTypeSpecificationRowObservationIdentity observation)
    {
        if (!MetadataTypeSpecificationPhysicalTableCatalogIdentity.OwnsRowMintCapability(mintCapability))
        {
            throw new ArgumentException("A physical TypeSpec row requires its complete table's private mint capability.");
        }
        return new MetadataTypeSpecificationPhysicalRowIdentity(sourceEnds, observation);
    }
}

/// <summary>Freezes one complete RID-ordered physical TypeSpec draft table.</summary>
/// <remarks>Physical row completeness remains independent of signature decode or graph traversal success.</remarks>
public sealed class MetadataTypeSpecificationPhysicalTableCatalogIdentity :
    IEquatable<MetadataTypeSpecificationPhysicalTableCatalogIdentity>
{
    /// <summary>Gets the deterministic draft bound name for physical TypeSpec rows.</summary>
    public const string TableRowCountBoundName = "metadata-v2.physical-typespec.rows";

    private const string CanonicalDomain = "metadata-v2-typespec-physical-table";
    private const int CanonicalSchemaVersion = 1;
    private static readonly object RowMintCapability = new();
    private readonly ImmutableArray<MetadataTypeSpecificationPhysicalRowIdentity> rows;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataTypeSpecificationPhysicalTableCatalogIdentity(
        MetadataReferencePhysicalTableResultKind resultKind,
        MetadataReferencePhysicalTableIssue issue,
        MetadataReferenceSourceEndIdentity sourceEnds,
        ImmutableArray<MetadataTypeSpecificationPhysicalRowIdentity> rows,
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

    /// <summary>Gets whether this physical TypeSpec draft table is exact, non-exact, or invalid.</summary>
    public MetadataReferencePhysicalTableResultKind ResultKind { get; }

    /// <summary>Gets the typed complete physical TypeSpec draft issue.</summary>
    public MetadataReferencePhysicalTableIssue Issue { get; }

    /// <summary>Gets the exact draft reference source ends for this table.</summary>
    public MetadataReferenceSourceEndIdentity SourceEnds { get; }

    /// <summary>Gets a defensive RID-order copy of exact guarded rows, or an empty vector for every stop.</summary>
    public ImmutableArray<MetadataTypeSpecificationPhysicalRowIdentity> Rows =>
        ExpressionV2ContractEncoding.Copy(rows);

    /// <summary>Gets the physical TypeSpec row-count draft bound after a cap-plus-one outcome.</summary>
    public EvaluationDeterministicBound? ReachedBound { get; }

    /// <summary>Gets the supplied, exact, or cap-plus-one draft observation count.</summary>
    public int ObservedCount { get; }

    /// <summary>Gets a defensive copy of the fixed-reference canonical draft table bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft table bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one complete RID-ordered physical TypeSpec draft table.</summary>
    /// <param name="sourceEnds">The exact reference source ends defining the physical table length.</param>
    /// <param name="observations">Every source-anchored physical TypeSpec row in RID order.</param>
    /// <returns>An exact guarded table or a prefix-free typed draft stop.</returns>
    public static MetadataTypeSpecificationPhysicalTableCatalogIdentity Create(
        MetadataReferenceSourceEndIdentity sourceEnds,
        ImmutableArray<MetadataTypeSpecificationRowObservationIdentity> observations)
    {
        ArgumentNullException.ThrowIfNull(sourceEnds);
        var issue = MetadataReferencePhysicalTableValidation.ValidateVector(
            sourceEnds.TypeSpecificationRowCount,
            observations,
            0x1B,
            static row => row.TypeSpecificationToken,
            out var resultKind,
            out var reachedBound,
            out var observedCount,
            TableRowCountBoundName);
        if (issue != MetadataReferencePhysicalTableIssue.None)
        {
            return new MetadataTypeSpecificationPhysicalTableCatalogIdentity(
                resultKind, issue, sourceEnds, [], reachedBound, observedCount);
        }
        if (observations.Any(row => !row.SourceModule.Equals(sourceEnds.SourceModule)))
        {
            return new MetadataTypeSpecificationPhysicalTableCatalogIdentity(
                MetadataReferencePhysicalTableResultKind.Invalid,
                MetadataReferencePhysicalTableIssue.SourceMismatch,
                sourceEnds,
                [],
                null,
                observations.Length);
        }
        var builder = ImmutableArray.CreateBuilder<MetadataTypeSpecificationPhysicalRowIdentity>(observations.Length);
        foreach (var observation in observations)
        {
            builder.Add(MetadataTypeSpecificationPhysicalRowIdentity.Create(RowMintCapability, sourceEnds, observation));
        }
        return new MetadataTypeSpecificationPhysicalTableCatalogIdentity(
            MetadataReferencePhysicalTableResultKind.Exact,
            MetadataReferencePhysicalTableIssue.None,
            sourceEnds,
            builder.MoveToImmutable(),
            null,
            observations.Length);
    }

    /// <summary>Finds one exact guarded physical TypeSpec draft row by token.</summary>
    /// <param name="typeSpecificationToken">The non-nil physical TypeSpec token.</param>
    /// <returns>The exact draft row, or null when the table or token is not exact.</returns>
    public MetadataTypeSpecificationPhysicalRowIdentity? FindRow(int typeSpecificationToken)
    {
        if (ResultKind != MetadataReferencePhysicalTableResultKind.Exact ||
            !CanonicalReplayEncoding.IsMetadataTokenForTable(typeSpecificationToken, 0x1B))
        {
            return null;
        }
        var rowId = CanonicalReplayEncoding.MetadataTokenRowId(typeSpecificationToken);
        return rowId > 0 && rowId <= rows.Length ? rows[rowId - 1] : null;
    }

    /// <summary>Tests canonical equality between two complete physical TypeSpec draft tables.</summary>
    /// <param name="other">The other draft table.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(MetadataTypeSpecificationPhysicalTableCatalogIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests complete physical TypeSpec draft-table equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a table with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataTypeSpecificationPhysicalTableCatalogIdentity);

    /// <summary>Computes a deterministic hash code from fixed-reference canonical draft content.</summary>
    /// <returns>A hash code for this complete physical TypeSpec draft table.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static bool OwnsRowMintCapability(object? capability) => ReferenceEquals(capability, RowMintCapability);
}

/// <summary>Freezes one source-anchored raw File-row draft observation.</summary>
/// <remarks>The physical shape covers metadata-bearing and ContainsNoMetadata rows without selecting a netmodule.</remarks>
public sealed class MetadataAssemblyFileRowObservationIdentity :
    IEquatable<MetadataAssemblyFileRowObservationIdentity>
{
    /// <summary>Gets the maximum physical File.HashValue byte count retained by this draft observation.</summary>
    public const int MaximumHashValueLength = StaticFieldAssemblyFileIdentity.MaximumHashValueLength;

    private const string CanonicalDomain = "metadata-v2-file-row-observation";
    private const int CanonicalSchemaVersion = 1;
    private const int ContainsNoMetadataFlag = 0x0001;
    private readonly ImmutableArray<byte> hashValue;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataAssemblyFileRowObservationIdentity(
        StaticFieldMetadataModuleIdentity sourceModule,
        int fileToken,
        int flags,
        string name,
        ImmutableArray<byte> hashValue)
    {
        SourceModule = sourceModule;
        FileToken = fileToken;
        Flags = flags;
        Name = name;
        this.hashValue = ExpressionV2ContractEncoding.Copy(hashValue);
        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteSha256(sourceModule.Sha256, nameof(sourceModule));
        writer.WriteInt32(fileToken);
        writer.WriteInt32(flags);
        writer.WriteString(name);
        writer.WriteLengthPrefixedBytes(hashValue.AsSpan());
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact metadata module containing the physical File row.</summary>
    public StaticFieldMetadataModuleIdentity SourceModule { get; }

    /// <summary>Gets the exact non-nil physical File token.</summary>
    public int FileToken { get; }

    /// <summary>Gets the exact raw File flags.</summary>
    public int Flags { get; }

    /// <summary>Gets the exact decoded non-empty File name.</summary>
    public string Name { get; }

    /// <summary>Gets whether the physical row declares that its file contains metadata.</summary>
    public bool ContainsMetadata => (Flags & ContainsNoMetadataFlag) == 0;

    /// <summary>Gets a defensive copy of the complete physical File hash-value blob.</summary>
    public ImmutableArray<byte> HashValue => ExpressionV2ContractEncoding.Copy(hashValue);

    /// <summary>Gets a defensive copy of the canonical physical draft observation bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft observation bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one source-anchored raw File-row draft observation.</summary>
    /// <param name="sourceModule">The exact metadata module containing the physical row.</param>
    /// <param name="fileToken">The exact non-nil File token.</param>
    /// <param name="flags">The exact raw File flags.</param>
    /// <param name="name">The exact decoded non-empty File name.</param>
    /// <param name="hashValue">The initialized bounded physical hash-value blob.</param>
    /// <returns>A physical W8 draft observation that covers either File flag disposition.</returns>
    public static MetadataAssemblyFileRowObservationIdentity Create(
        StaticFieldMetadataModuleIdentity sourceModule,
        int fileToken,
        int flags,
        string name,
        ImmutableArray<byte> hashValue)
    {
        ArgumentNullException.ThrowIfNull(sourceModule);
        CanonicalReplayEncoding.ValidateMetadataToken(fileToken, 0x26, nameof(fileToken));
        if ((flags & ~ContainsNoMetadataFlag) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(flags));
        }
        StaticFieldMetadataTextLimits.Validate(name, nameof(name), allowEmpty: false);
        if (hashValue.IsDefault || hashValue.Length > MaximumHashValueLength)
        {
            throw new ArgumentException(
                $"An initialized File hash of at most {MaximumHashValueLength} bytes is required.",
                nameof(hashValue));
        }
        return new MetadataAssemblyFileRowObservationIdentity(sourceModule, fileToken, flags, name, hashValue);
    }

    /// <summary>Tests whether this physical draft observation agrees with one metadata-bearing W7 File candidate.</summary>
    /// <param name="candidate">The W7 metadata-bearing File candidate.</param>
    /// <returns><see langword="true"/> only when every physical row field agrees.</returns>
    public bool MatchesCandidate(StaticFieldAssemblyFileIdentity candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        return ContainsMetadata && FileToken == candidate.FileToken && Flags == candidate.Flags &&
            StringComparer.Ordinal.Equals(Name, candidate.Name) &&
            hashValue.AsSpan().SequenceEqual(candidate.HashValue.AsSpan());
    }

    /// <summary>Tests canonical equality between two raw File draft observations.</summary>
    /// <param name="other">The other draft observation.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(MetadataAssemblyFileRowObservationIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests raw File draft observation equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for an observation with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataAssemblyFileRowObservationIdentity);

    /// <summary>Computes a deterministic hash code from canonical physical draft content.</summary>
    /// <returns>A hash code for this raw File draft observation.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Freezes one guarded physical File draft row.</summary>
/// <remarks>Only an exact complete File table can mint this fixed-reference draft row.</remarks>
public sealed class MetadataAssemblyFilePhysicalRowIdentity :
    IEquatable<MetadataAssemblyFilePhysicalRowIdentity>
{
    private const string CanonicalDomain = "metadata-v2-file-physical-row";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataAssemblyFilePhysicalRowIdentity(
        MetadataReferenceSourceEndIdentity sourceEnds,
        MetadataAssemblyFileRowObservationIdentity observation)
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

    /// <summary>Gets the unchanged raw File-row draft observation.</summary>
    public MetadataAssemblyFileRowObservationIdentity Observation { get; }

    /// <summary>Gets the exact non-nil physical File token.</summary>
    public int FileToken => Observation.FileToken;

    /// <summary>Gets a defensive copy of the fixed-reference canonical draft row bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft row bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Tests canonical equality between two guarded physical File draft rows.</summary>
    /// <param name="other">The other draft row.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(MetadataAssemblyFilePhysicalRowIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests guarded physical File draft equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a row with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataAssemblyFilePhysicalRowIdentity);

    /// <summary>Computes a deterministic hash code from fixed-reference canonical draft content.</summary>
    /// <returns>A hash code for this guarded physical File draft row.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static MetadataAssemblyFilePhysicalRowIdentity Create(
        object mintCapability,
        MetadataReferenceSourceEndIdentity sourceEnds,
        MetadataAssemblyFileRowObservationIdentity observation)
    {
        if (!MetadataAssemblyFilePhysicalTableCatalogIdentity.OwnsRowMintCapability(mintCapability))
        {
            throw new ArgumentException("A physical File row requires its complete table's private mint capability.");
        }
        return new MetadataAssemblyFilePhysicalRowIdentity(sourceEnds, observation);
    }
}

/// <summary>Freezes one complete RID-ordered physical File draft table.</summary>
/// <remarks>Metadata-bearing and ContainsNoMetadata rows participate equally in complete coverage.</remarks>
public sealed class MetadataAssemblyFilePhysicalTableCatalogIdentity :
    IEquatable<MetadataAssemblyFilePhysicalTableCatalogIdentity>
{
    /// <summary>Gets the deterministic draft bound name for physical File rows.</summary>
    public const string TableRowCountBoundName = "metadata-v2.physical-file.rows";

    private const string CanonicalDomain = "metadata-v2-file-physical-table";
    private const int CanonicalSchemaVersion = 1;
    private static readonly object RowMintCapability = new();
    private readonly ImmutableArray<MetadataAssemblyFilePhysicalRowIdentity> rows;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataAssemblyFilePhysicalTableCatalogIdentity(
        MetadataReferencePhysicalTableResultKind resultKind,
        MetadataReferencePhysicalTableIssue issue,
        MetadataReferenceSourceEndIdentity sourceEnds,
        ImmutableArray<MetadataAssemblyFilePhysicalRowIdentity> rows,
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

    /// <summary>Gets whether this physical File draft table is exact, non-exact, or invalid.</summary>
    public MetadataReferencePhysicalTableResultKind ResultKind { get; }

    /// <summary>Gets the typed complete physical File draft issue.</summary>
    public MetadataReferencePhysicalTableIssue Issue { get; }

    /// <summary>Gets the exact draft reference source ends for this table.</summary>
    public MetadataReferenceSourceEndIdentity SourceEnds { get; }

    /// <summary>Gets a defensive RID-order copy of exact guarded rows, or an empty vector for every stop.</summary>
    public ImmutableArray<MetadataAssemblyFilePhysicalRowIdentity> Rows =>
        ExpressionV2ContractEncoding.Copy(rows);

    /// <summary>Gets the physical File row-count draft bound after a cap-plus-one outcome.</summary>
    public EvaluationDeterministicBound? ReachedBound { get; }

    /// <summary>Gets the supplied, exact, or cap-plus-one draft observation count.</summary>
    public int ObservedCount { get; }

    /// <summary>Gets a defensive copy of the fixed-reference canonical draft table bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft table bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one complete RID-ordered physical File draft table.</summary>
    /// <param name="sourceEnds">The exact reference source ends defining the physical table length.</param>
    /// <param name="observations">Every source-anchored raw physical File row in RID order.</param>
    /// <returns>An exact guarded table or a prefix-free typed draft stop.</returns>
    public static MetadataAssemblyFilePhysicalTableCatalogIdentity Create(
        MetadataReferenceSourceEndIdentity sourceEnds,
        ImmutableArray<MetadataAssemblyFileRowObservationIdentity> observations)
    {
        ArgumentNullException.ThrowIfNull(sourceEnds);
        var issue = MetadataReferencePhysicalTableValidation.ValidateVector(
            sourceEnds.FileRowCount,
            observations,
            0x26,
            static row => row.FileToken,
            out var resultKind,
            out var reachedBound,
            out var observedCount,
            TableRowCountBoundName);
        if (issue != MetadataReferencePhysicalTableIssue.None)
        {
            return new MetadataAssemblyFilePhysicalTableCatalogIdentity(
                resultKind, issue, sourceEnds, [], reachedBound, observedCount);
        }
        if (observations.Any(row => !row.SourceModule.Equals(sourceEnds.SourceModule)))
        {
            return new MetadataAssemblyFilePhysicalTableCatalogIdentity(
                MetadataReferencePhysicalTableResultKind.Invalid,
                MetadataReferencePhysicalTableIssue.SourceMismatch,
                sourceEnds,
                [],
                null,
                observations.Length);
        }
        var builder = ImmutableArray.CreateBuilder<MetadataAssemblyFilePhysicalRowIdentity>(observations.Length);
        foreach (var observation in observations)
        {
            builder.Add(MetadataAssemblyFilePhysicalRowIdentity.Create(RowMintCapability, sourceEnds, observation));
        }
        return new MetadataAssemblyFilePhysicalTableCatalogIdentity(
            MetadataReferencePhysicalTableResultKind.Exact,
            MetadataReferencePhysicalTableIssue.None,
            sourceEnds,
            builder.MoveToImmutable(),
            null,
            observations.Length);
    }

    /// <summary>Finds one exact guarded physical File draft row by token.</summary>
    /// <param name="fileToken">The non-nil physical File token.</param>
    /// <returns>The exact draft row, or null when the table or token is not exact.</returns>
    public MetadataAssemblyFilePhysicalRowIdentity? FindRow(int fileToken)
    {
        if (ResultKind != MetadataReferencePhysicalTableResultKind.Exact ||
            !CanonicalReplayEncoding.IsMetadataTokenForTable(fileToken, 0x26))
        {
            return null;
        }
        var rowId = CanonicalReplayEncoding.MetadataTokenRowId(fileToken);
        return rowId > 0 && rowId <= rows.Length ? rows[rowId - 1] : null;
    }

    /// <summary>Tests canonical equality between two complete physical File draft tables.</summary>
    /// <param name="other">The other draft table.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(MetadataAssemblyFilePhysicalTableCatalogIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests complete physical File draft-table equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a table with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataAssemblyFilePhysicalTableCatalogIdentity);

    /// <summary>Computes a deterministic hash code from fixed-reference canonical draft content.</summary>
    /// <returns>A hash code for this complete physical File draft table.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static bool OwnsRowMintCapability(object? capability) => ReferenceEquals(capability, RowMintCapability);
}

/// <summary>Freezes one source-anchored raw ExportedType-row draft observation.</summary>
/// <remarks>
/// The physical shape retains File, AssemblyRef, and enclosing-ExportedType Implementation forms. It does not select
/// a target assembly or assume the row is a top-level forwarder.
/// </remarks>
public sealed class MetadataExportedTypeRowObservationIdentity :
    IEquatable<MetadataExportedTypeRowObservationIdentity>
{
    private const string CanonicalDomain = "metadata-v2-exportedtype-row-observation";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataExportedTypeRowObservationIdentity(
        StaticFieldMetadataModuleIdentity sourceModule,
        int exportedTypeToken,
        int typeAttributes,
        int typeDefinitionId,
        string namespaceName,
        string typeName,
        int implementationMetadataToken)
    {
        SourceModule = sourceModule;
        ExportedTypeToken = exportedTypeToken;
        TypeAttributes = typeAttributes;
        TypeDefinitionId = typeDefinitionId;
        NamespaceName = namespaceName;
        TypeName = typeName;
        ImplementationMetadataToken = implementationMetadataToken;
        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteSha256(sourceModule.Sha256, nameof(sourceModule));
        writer.WriteInt32(exportedTypeToken);
        writer.WriteInt32(typeAttributes);
        writer.WriteInt32(typeDefinitionId);
        writer.WriteString(namespaceName);
        writer.WriteString(typeName);
        writer.WriteInt32(implementationMetadataToken);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact metadata module containing the physical ExportedType row.</summary>
    public StaticFieldMetadataModuleIdentity SourceModule { get; }

    /// <summary>Gets the exact non-nil physical ExportedType token.</summary>
    public int ExportedTypeToken { get; }

    /// <summary>Gets the exact raw ExportedType attributes.</summary>
    public int TypeAttributes { get; }

    /// <summary>Gets the exact raw ExportedType.TypeDefId value.</summary>
    public int TypeDefinitionId { get; }

    /// <summary>Gets the exact decoded namespace, including empty for nested rows.</summary>
    public string NamespaceName { get; }

    /// <summary>Gets the exact decoded non-empty metadata type name.</summary>
    public string TypeName { get; }

    /// <summary>Gets the exact non-nil File, AssemblyRef, or ExportedType Implementation token.</summary>
    public int ImplementationMetadataToken { get; }

    /// <summary>Gets a defensive copy of the canonical physical draft observation bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft observation bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one source-anchored raw ExportedType-row draft observation.</summary>
    /// <param name="sourceModule">The exact manifest metadata module containing the physical row.</param>
    /// <param name="exportedTypeToken">The exact non-nil ExportedType token.</param>
    /// <param name="typeAttributes">The exact raw ExportedType attributes.</param>
    /// <param name="typeDefinitionId">The exact non-negative raw TypeDefId value.</param>
    /// <param name="namespaceName">The exact namespace, including empty for a nested row.</param>
    /// <param name="typeName">The exact non-empty metadata type name.</param>
    /// <param name="implementationMetadataToken">The non-nil File, AssemblyRef, or ExportedType token.</param>
    /// <returns>A raw W8 draft observation with no selected target assembly.</returns>
    public static MetadataExportedTypeRowObservationIdentity Create(
        StaticFieldMetadataModuleIdentity sourceModule,
        int exportedTypeToken,
        int typeAttributes,
        int typeDefinitionId,
        string namespaceName,
        string typeName,
        int implementationMetadataToken)
    {
        ArgumentNullException.ThrowIfNull(sourceModule);
        CanonicalReplayEncoding.ValidateMetadataToken(exportedTypeToken, 0x27, nameof(exportedTypeToken));
        ArgumentOutOfRangeException.ThrowIfNegative(typeDefinitionId);
        StaticFieldMetadataTextLimits.Validate(namespaceName, nameof(namespaceName), allowEmpty: true);
        if (namespaceName.Length > 0)
        {
            StaticFieldContractEncoding.RequireIdentifierValue(namespaceName, nameof(namespaceName), allowDots: true);
        }
        StaticFieldMetadataTextLimits.Validate(typeName, nameof(typeName), allowEmpty: false);
        StaticFieldContractEncoding.RequireIdentifierValue(typeName, nameof(typeName), allowDots: false);
        if (!CanonicalReplayEncoding.IsMetadataTokenForTable(implementationMetadataToken, 0x23) &&
            !CanonicalReplayEncoding.IsMetadataTokenForTable(implementationMetadataToken, 0x26) &&
            !CanonicalReplayEncoding.IsMetadataTokenForTable(implementationMetadataToken, 0x27))
        {
            throw new ArgumentOutOfRangeException(nameof(implementationMetadataToken));
        }
        return new MetadataExportedTypeRowObservationIdentity(
            sourceModule,
            exportedTypeToken,
            typeAttributes,
            typeDefinitionId,
            namespaceName,
            typeName,
            implementationMetadataToken);
    }

    /// <summary>Tests whether this physical row agrees with one W7 top-level forwarder candidate.</summary>
    /// <param name="candidate">The W7 forwarder candidate to compare.</param>
    /// <returns><see langword="true"/> only when every physical row field and source assembly agree.</returns>
    public bool MatchesCandidate(StaticFieldExportedTypeForwarderIdentity candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        return SourceModule.ContainingAssembly.Equals(candidate.SourceAssembly) &&
            ExportedTypeToken == candidate.ExportedTypeToken &&
            TypeAttributes == candidate.TypeAttributes && TypeDefinitionId == candidate.TypeDefinitionId &&
            StringComparer.Ordinal.Equals(NamespaceName, candidate.NamespaceName) &&
            StringComparer.Ordinal.Equals(TypeName, candidate.TypeName) &&
            ImplementationMetadataToken == candidate.ImplementationAssemblyReference.AssemblyReferenceToken;
    }

    /// <summary>Tests canonical equality between two raw ExportedType draft observations.</summary>
    /// <param name="other">The other draft observation.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(MetadataExportedTypeRowObservationIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests raw ExportedType draft observation equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for an observation with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataExportedTypeRowObservationIdentity);

    /// <summary>Computes a deterministic hash code from canonical physical draft content.</summary>
    /// <returns>A hash code for this raw ExportedType draft observation.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Freezes one guarded physical ExportedType draft row.</summary>
/// <remarks>Only an exact complete ExportedType table can mint this fixed-reference draft row.</remarks>
public sealed class MetadataExportedTypePhysicalRowIdentity :
    IEquatable<MetadataExportedTypePhysicalRowIdentity>
{
    private const string CanonicalDomain = "metadata-v2-exportedtype-physical-row";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataExportedTypePhysicalRowIdentity(
        MetadataReferenceSourceEndIdentity sourceEnds,
        MetadataExportedTypeRowObservationIdentity observation)
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

    /// <summary>Gets the unchanged raw ExportedType-row draft observation.</summary>
    public MetadataExportedTypeRowObservationIdentity Observation { get; }

    /// <summary>Gets the exact non-nil physical ExportedType token.</summary>
    public int ExportedTypeToken => Observation.ExportedTypeToken;

    /// <summary>Gets a defensive copy of the fixed-reference canonical draft row bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft row bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Tests canonical equality between two guarded physical ExportedType draft rows.</summary>
    /// <param name="other">The other draft row.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(MetadataExportedTypePhysicalRowIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests guarded physical ExportedType draft equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a row with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataExportedTypePhysicalRowIdentity);

    /// <summary>Computes a deterministic hash code from fixed-reference canonical draft content.</summary>
    /// <returns>A hash code for this guarded physical ExportedType draft row.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static MetadataExportedTypePhysicalRowIdentity Create(
        object mintCapability,
        MetadataReferenceSourceEndIdentity sourceEnds,
        MetadataExportedTypeRowObservationIdentity observation)
    {
        if (!MetadataExportedTypePhysicalTableCatalogIdentity.OwnsRowMintCapability(mintCapability))
        {
            throw new ArgumentException("A physical ExportedType row requires its complete table's private mint capability.");
        }
        return new MetadataExportedTypePhysicalRowIdentity(sourceEnds, observation);
    }
}

/// <summary>Freezes one complete RID-ordered physical ExportedType draft table.</summary>
/// <remarks>File, AssemblyRef, and enclosing-ExportedType implementations are range-checked before issuance.</remarks>
public sealed class MetadataExportedTypePhysicalTableCatalogIdentity :
    IEquatable<MetadataExportedTypePhysicalTableCatalogIdentity>
{
    /// <summary>Gets the deterministic draft bound name for physical ExportedType rows.</summary>
    public const string TableRowCountBoundName = "metadata-v2.physical-exportedtype.rows";

    private const string CanonicalDomain = "metadata-v2-exportedtype-physical-table";
    private const int CanonicalSchemaVersion = 1;
    private static readonly object RowMintCapability = new();
    private readonly ImmutableArray<MetadataExportedTypePhysicalRowIdentity> rows;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataExportedTypePhysicalTableCatalogIdentity(
        MetadataReferencePhysicalTableResultKind resultKind,
        MetadataReferencePhysicalTableIssue issue,
        MetadataReferenceSourceEndIdentity sourceEnds,
        ImmutableArray<MetadataExportedTypePhysicalRowIdentity> rows,
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

    /// <summary>Gets whether this physical ExportedType draft table is exact, non-exact, or invalid.</summary>
    public MetadataReferencePhysicalTableResultKind ResultKind { get; }

    /// <summary>Gets the typed complete physical ExportedType draft issue.</summary>
    public MetadataReferencePhysicalTableIssue Issue { get; }

    /// <summary>Gets the exact draft reference source ends for this table.</summary>
    public MetadataReferenceSourceEndIdentity SourceEnds { get; }

    /// <summary>Gets a defensive RID-order copy of exact guarded rows, or an empty vector for every stop.</summary>
    public ImmutableArray<MetadataExportedTypePhysicalRowIdentity> Rows =>
        ExpressionV2ContractEncoding.Copy(rows);

    /// <summary>Gets the physical ExportedType row-count draft bound after a cap-plus-one outcome.</summary>
    public EvaluationDeterministicBound? ReachedBound { get; }

    /// <summary>Gets the supplied, exact, or cap-plus-one draft observation count.</summary>
    public int ObservedCount { get; }

    /// <summary>Gets a defensive copy of the fixed-reference canonical draft table bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft table bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one complete RID-ordered physical ExportedType draft table.</summary>
    /// <param name="sourceEnds">The exact reference source ends defining the physical table and implementation ends.</param>
    /// <param name="observations">Every source-anchored raw ExportedType row in RID order.</param>
    /// <returns>An exact guarded table or a prefix-free typed draft stop.</returns>
    public static MetadataExportedTypePhysicalTableCatalogIdentity Create(
        MetadataReferenceSourceEndIdentity sourceEnds,
        ImmutableArray<MetadataExportedTypeRowObservationIdentity> observations)
    {
        ArgumentNullException.ThrowIfNull(sourceEnds);
        var issue = MetadataReferencePhysicalTableValidation.ValidateVector(
            sourceEnds.ExportedTypeRowCount,
            observations,
            0x27,
            static row => row.ExportedTypeToken,
            out var resultKind,
            out var reachedBound,
            out var observedCount,
            TableRowCountBoundName);
        if (issue != MetadataReferencePhysicalTableIssue.None)
        {
            return new MetadataExportedTypePhysicalTableCatalogIdentity(
                resultKind, issue, sourceEnds, [], reachedBound, observedCount);
        }
        if (observations.Any(row => !row.SourceModule.Equals(sourceEnds.SourceModule)))
        {
            return new MetadataExportedTypePhysicalTableCatalogIdentity(
                MetadataReferencePhysicalTableResultKind.Invalid,
                MetadataReferencePhysicalTableIssue.SourceMismatch,
                sourceEnds,
                [],
                null,
                observations.Length);
        }
        if (observations.Any(row => !sourceEnds.ContainsReferenceToken(row.ImplementationMetadataToken)))
        {
            return new MetadataExportedTypePhysicalTableCatalogIdentity(
                MetadataReferencePhysicalTableResultKind.Invalid,
                MetadataReferencePhysicalTableIssue.CodedIndexOutOfRange,
                sourceEnds,
                [],
                null,
                observations.Length);
        }
        var builder = ImmutableArray.CreateBuilder<MetadataExportedTypePhysicalRowIdentity>(observations.Length);
        foreach (var observation in observations)
        {
            builder.Add(MetadataExportedTypePhysicalRowIdentity.Create(RowMintCapability, sourceEnds, observation));
        }
        return new MetadataExportedTypePhysicalTableCatalogIdentity(
            MetadataReferencePhysicalTableResultKind.Exact,
            MetadataReferencePhysicalTableIssue.None,
            sourceEnds,
            builder.MoveToImmutable(),
            null,
            observations.Length);
    }

    /// <summary>Finds one exact guarded physical ExportedType draft row by token.</summary>
    /// <param name="exportedTypeToken">The non-nil physical ExportedType token.</param>
    /// <returns>The exact draft row, or null when the table or token is not exact.</returns>
    public MetadataExportedTypePhysicalRowIdentity? FindRow(int exportedTypeToken)
    {
        if (ResultKind != MetadataReferencePhysicalTableResultKind.Exact ||
            !CanonicalReplayEncoding.IsMetadataTokenForTable(exportedTypeToken, 0x27))
        {
            return null;
        }
        var rowId = CanonicalReplayEncoding.MetadataTokenRowId(exportedTypeToken);
        return rowId > 0 && rowId <= rows.Length ? rows[rowId - 1] : null;
    }

    /// <summary>Tests canonical equality between two complete physical ExportedType draft tables.</summary>
    /// <param name="other">The other draft table.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(MetadataExportedTypePhysicalTableCatalogIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests complete physical ExportedType draft-table equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a table with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataExportedTypePhysicalTableCatalogIdentity);

    /// <summary>Computes a deterministic hash code from fixed-reference canonical draft content.</summary>
    /// <returns>A hash code for this complete physical ExportedType draft table.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static bool OwnsRowMintCapability(object? capability) => ReferenceEquals(capability, RowMintCapability);
}
