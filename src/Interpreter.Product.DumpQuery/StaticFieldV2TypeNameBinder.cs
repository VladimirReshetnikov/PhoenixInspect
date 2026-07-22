using System.Collections.Immutable;
using Interpreter.Core.Abstractions;

namespace Interpreter.Product.DumpQuery;

/// <summary>Identifies why one examined draft chain did not match a partition's owner name segments.</summary>
/// <remarks>
/// Every value is a retained per-occurrence draft fact rather than a binding failure. The reasons are ordered by how
/// far the examined chain progressed through the fixed comparison sequence: shape, module pseudo-type, namespace
/// text, exact compiler mapping, projected simple name, introduced arity, and ordinary C# addressability.
/// </remarks>
public enum StaticFieldV2TypeNameRejectionReason
{
    /// <summary>The chain's outermost namespace text differed from the examined namespace split.</summary>
    NamespaceMismatch = 1,

    /// <summary>One projected simple name differed from the corresponding syntax identifier.</summary>
    SimpleNameMismatch = 2,

    /// <summary>One introduced generic arity differed from the corresponding syntax arity.</summary>
    ArityMismatch = 3,

    /// <summary>One segment's compiler-name mapping was non-exact and can never spell a source name.</summary>
    NonExactCompilerMapping = 4,

    /// <summary>One segment has no ordinary C# simple-name spelling.</summary>
    NotCSharpAddressable = 5,

    /// <summary>The chain identifies the ECMA module pseudo-type, which is never a named source type.</summary>
    ModulePseudoType = 6,

    /// <summary>The chain segment count disagreed with the examined namespace/type split.</summary>
    SegmentCountMismatch = 7,
}

/// <summary>Classifies one draft namespace/type name-binding answer for a partition or a complete expression.</summary>
/// <remarks>
/// <see cref="Exact"/>, <see cref="Absent"/>, and <see cref="Ambiguous"/> are complete derived draft answers that
/// retain their per-partition evidence. <see cref="NonExact"/>, <see cref="Invalid"/>, and <see cref="Unsupported"/>
/// are prefix-free stops that retain no partition evidence and no selected candidate.
/// </remarks>
public enum StaticFieldV2TypeNameBindingResultKind
{
    /// <summary>Exactly one physical draft candidate group was derived.</summary>
    Exact = 1,

    /// <summary>Every examined draft occurrence was rejected, so no candidate exists.</summary>
    Absent = 2,

    /// <summary>Two or more distinct physical draft candidate groups were derived.</summary>
    Ambiguous = 3,

    /// <summary>A prerequisite or a declared draft bound prevented a complete answer.</summary>
    NonExact = 4,

    /// <summary>A complete draft prerequisite contradicted itself.</summary>
    Invalid = 5,

    /// <summary>The expression selects a later draft route that this explicit-route binder does not own.</summary>
    Unsupported = 6,
}

/// <summary>Identifies the deterministic issue for one draft namespace/type name-binding outcome.</summary>
/// <remarks>This draft-phase issue catalog keeps prerequisite, route, bound, and derived answers distinct.</remarks>
public enum StaticFieldV2TypeNameBindingIssue
{
    /// <summary>No issue applies to an exact draft outcome.</summary>
    None = 0,

    /// <summary>The named-TypeDef-chain portfolio prerequisite was non-exact.</summary>
    ChainPortfolioNonExact = 1,

    /// <summary>The named-TypeDef-chain portfolio prerequisite was invalid.</summary>
    ChainPortfolioInvalid = 2,

    /// <summary>The examined candidate-occurrence count reached the declared draft cap plus one.</summary>
    CandidateOccurrenceBoundReached = 3,

    /// <summary>The grouped physical-candidate count reached the declared draft cap plus one.</summary>
    CandidateGroupBoundReached = 4,

    /// <summary>A named alias qualifier requires the separately owned scoped-context draft route.</summary>
    ContextRouteNotSupported = 5,

    /// <summary>The descriptor retained no qualified-owner partition for the explicit draft route.</summary>
    NoQualifiedOwnerPartition = 6,

    /// <summary>Two or more partitions derived different physical draft candidate groups.</summary>
    AmbiguousAcrossPartitions = 7,

    /// <summary>One partition derived two or more physical draft candidate groups.</summary>
    AmbiguousWithinPartition = 8,

    /// <summary>Every examined draft occurrence was rejected in every qualified-owner partition.</summary>
    CandidateAbsent = 9,
}

/// <summary>Freezes one accepted draft match between a partition's owner name and an authority-issued chain.</summary>
/// <remarks>
/// The occurrence is minted only by <see cref="StaticFieldV2TypeNameBindingOutcome"/>. It retains the exact metadata
/// module, the exact draft chain, the examined namespace split, and the descriptor partition that produced it. No
/// type argument is constructed and no member is looked up by this draft row.
/// </remarks>
public sealed class StaticFieldV2TypeNameCandidateOccurrence : IEquatable<StaticFieldV2TypeNameCandidateOccurrence>
{
    private const string CanonicalDomain = "static-field-v2-type-name-candidate-occurrence";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldV2TypeNameCandidateOccurrence(
        StaticFieldMetadataModuleIdentity sourceModule,
        MetadataNamedTypeDefinitionChainIdentity chain,
        int namespaceSegmentCount,
        int partitionIndex)
    {
        SourceModule = sourceModule;
        Chain = chain;
        NamespaceSegmentCount = namespaceSegmentCount;
        PartitionIndex = partitionIndex;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteSha256(sourceModule.Sha256, nameof(sourceModule));
        writer.WriteSha256(chain.Sha256, nameof(chain));
        writer.WriteInt32(namespaceSegmentCount);
        writer.WriteInt32(partitionIndex);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact metadata module whose draft chain catalog produced this occurrence.</summary>
    public StaticFieldMetadataModuleIdentity SourceModule { get; }

    /// <summary>Gets the exact authority-derived outer-to-inner named-TypeDef draft chain that matched.</summary>
    public MetadataNamedTypeDefinitionChainIdentity Chain { get; }

    /// <summary>Gets the leading owner-segment count interpreted as namespace text by this occurrence.</summary>
    public int NamespaceSegmentCount { get; }

    /// <summary>Gets the zero-based descriptor partition index that produced this occurrence.</summary>
    public int PartitionIndex { get; }

    /// <summary>Gets the final authority-issued TypeDef named by the matched draft chain.</summary>
    public MetadataTypeDefinitionAuthorityIdentity FinalTypeDefinition => Chain.FinalTypeDefinition;

    /// <summary>Gets the non-nil final TypeDef token used as half of the physical draft group key.</summary>
    public int FinalTypeDefinitionToken => Chain.FinalTypeDefinitionToken;

    /// <summary>Gets a defensive copy of this fixed-reference canonical draft occurrence.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft occurrence.</summary>
    public string Sha256 { get; }

    /// <summary>Tests canonical equality between two draft candidate occurrences.</summary>
    /// <param name="other">The other draft occurrence.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(StaticFieldV2TypeNameCandidateOccurrence? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests draft candidate-occurrence equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for an occurrence with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldV2TypeNameCandidateOccurrence);

    /// <summary>Computes a deterministic hash code from immutable canonical draft occurrence content.</summary>
    /// <returns>A hash code for this canonical draft occurrence.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static StaticFieldV2TypeNameCandidateOccurrence Create(
        object mintCapability,
        StaticFieldMetadataModuleIdentity sourceModule,
        MetadataNamedTypeDefinitionChainIdentity chain,
        int namespaceSegmentCount,
        int partitionIndex)
    {
        if (!StaticFieldV2TypeNameBindingOutcome.OwnsRowMintCapability(mintCapability))
        {
            throw new ArgumentException(
                "A type-name candidate occurrence requires the binding outcome's private mint capability.",
                nameof(mintCapability));
        }

        ArgumentNullException.ThrowIfNull(sourceModule);
        ArgumentNullException.ThrowIfNull(chain);
        ArgumentOutOfRangeException.ThrowIfNegative(namespaceSegmentCount);
        ArgumentOutOfRangeException.ThrowIfNegative(partitionIndex);
        if (chain.IsModulePseudoType)
        {
            throw new ArgumentException(
                "The module pseudo-type can never be an accepted named-type candidate.",
                nameof(chain));
        }

        return new StaticFieldV2TypeNameCandidateOccurrence(
            sourceModule,
            chain,
            namespaceSegmentCount,
            partitionIndex);
    }
}

/// <summary>Freezes one physical draft candidate: a metadata module and one final authority TypeDef.</summary>
/// <remarks>
/// Occurrences that name the same physical pair converge into exactly one group. Distinct pairs remain distinct
/// groups even when their spelled source names are identical, which is what makes cross-module ambiguity visible.
/// </remarks>
public sealed class StaticFieldV2TypeNameCandidateGroup : IEquatable<StaticFieldV2TypeNameCandidateGroup>
{
    private const string CanonicalDomain = "static-field-v2-type-name-candidate-group";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<StaticFieldV2TypeNameCandidateOccurrence> occurrences;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldV2TypeNameCandidateGroup(
        StaticFieldMetadataModuleIdentity sourceModule,
        MetadataTypeDefinitionAuthorityIdentity finalTypeDefinition,
        ImmutableArray<StaticFieldV2TypeNameCandidateOccurrence> occurrences)
    {
        SourceModule = sourceModule;
        FinalTypeDefinition = finalTypeDefinition;
        this.occurrences = ExpressionV2ContractEncoding.Copy(occurrences);

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteSha256(sourceModule.Sha256, nameof(sourceModule));
        writer.WriteSha256(finalTypeDefinition.Sha256, nameof(finalTypeDefinition));
        writer.WriteInt32(occurrences.Length);
        foreach (var occurrence in occurrences)
        {
            writer.WriteSha256(occurrence.Sha256, nameof(occurrences));
        }
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact metadata module that owns this physical draft candidate.</summary>
    public StaticFieldMetadataModuleIdentity SourceModule { get; }

    /// <summary>Gets the exact final authority-issued TypeDef of this physical draft candidate.</summary>
    public MetadataTypeDefinitionAuthorityIdentity FinalTypeDefinition { get; }

    /// <summary>Gets the non-nil final TypeDef token of this physical draft candidate.</summary>
    public int FinalTypeDefinitionToken => FinalTypeDefinition.TypeDefinitionToken;

    /// <summary>Gets a defensive copy of every converged draft occurrence in examination order.</summary>
    public ImmutableArray<StaticFieldV2TypeNameCandidateOccurrence> Occurrences =>
        ExpressionV2ContractEncoding.Copy(occurrences);

    /// <summary>Gets a defensive copy of this fixed-reference canonical draft group.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft group.</summary>
    public string Sha256 { get; }

    /// <summary>Tests canonical equality between two physical draft candidate groups.</summary>
    /// <param name="other">The other draft group.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(StaticFieldV2TypeNameCandidateGroup? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests physical draft candidate-group equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a group with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldV2TypeNameCandidateGroup);

    /// <summary>Computes a deterministic hash code from immutable canonical draft group content.</summary>
    /// <returns>A hash code for this canonical draft group.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static StaticFieldV2TypeNameCandidateGroup Create(
        object mintCapability,
        StaticFieldMetadataModuleIdentity sourceModule,
        MetadataTypeDefinitionAuthorityIdentity finalTypeDefinition,
        ImmutableArray<StaticFieldV2TypeNameCandidateOccurrence> occurrences)
    {
        if (!StaticFieldV2TypeNameBindingOutcome.OwnsRowMintCapability(mintCapability))
        {
            throw new ArgumentException(
                "A type-name candidate group requires the binding outcome's private mint capability.",
                nameof(mintCapability));
        }

        ArgumentNullException.ThrowIfNull(sourceModule);
        ArgumentNullException.ThrowIfNull(finalTypeDefinition);
        var copied = ExpressionV2ContractEncoding.CopyRequired(
            occurrences,
            nameof(occurrences),
            StaticFieldV2Limits.MaximumNameCandidateOccurrenceCount + 1);
        if (copied.IsEmpty)
        {
            throw new ArgumentException("A physical candidate group requires at least one occurrence.", nameof(occurrences));
        }
        foreach (var occurrence in copied)
        {
            if (!occurrence.SourceModule.Equals(sourceModule) ||
                !occurrence.FinalTypeDefinition.Equals(finalTypeDefinition))
            {
                throw new ArgumentException(
                    "Every converged occurrence must name one identical physical module and TypeDef pair.",
                    nameof(occurrences));
            }
        }

        return new StaticFieldV2TypeNameCandidateGroup(sourceModule, finalTypeDefinition, copied);
    }
}

/// <summary>Freezes the complete draft name-binding answer derived for one qualified-owner partition.</summary>
/// <remarks>
/// The result retains every physical draft group derived by the exhaustive namespace-versus-nested enumeration, the
/// count of occurrences examined for this partition, and one typed rejection reason retained as evidence. The
/// retained reason is the first reason observed among the occurrences that progressed furthest through the fixed
/// comparison sequence, so a mere shape disagreement never hides real name evidence.
/// </remarks>
public sealed class StaticFieldV2TypeNamePartitionResult : IEquatable<StaticFieldV2TypeNamePartitionResult>
{
    private const string CanonicalDomain = "static-field-v2-type-name-partition-result";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<StaticFieldV2TypeNameCandidateGroup> groups;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldV2TypeNamePartitionResult(
        int partitionIndex,
        StaticFieldV2CandidateKind candidateKind,
        int ownerSegmentCount,
        StaticFieldV2TypeNameBindingResultKind resultKind,
        ImmutableArray<StaticFieldV2TypeNameCandidateGroup> groups,
        int examinedOccurrenceCount,
        StaticFieldV2TypeNameRejectionReason? firstRejectionReason)
    {
        PartitionIndex = partitionIndex;
        CandidateKind = candidateKind;
        OwnerSegmentCount = ownerSegmentCount;
        ResultKind = resultKind;
        this.groups = ExpressionV2ContractEncoding.Copy(groups);
        ExaminedOccurrenceCount = examinedOccurrenceCount;
        FirstRejectionReason = firstRejectionReason;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32(partitionIndex);
        writer.WriteInt32((int)candidateKind);
        writer.WriteInt32(ownerSegmentCount);
        writer.WriteInt32((int)resultKind);
        writer.WriteInt32(groups.Length);
        foreach (var group in groups)
        {
            writer.WriteSha256(group.Sha256, nameof(groups));
        }
        writer.WriteInt32(examinedOccurrenceCount);
        ExpressionV2ContractEncoding.WriteOptionalEnum(writer, firstRejectionReason);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the zero-based descriptor partition index described by this draft result.</summary>
    public int PartitionIndex { get; }

    /// <summary>Gets the descriptor candidate kind, which is always a qualified owner for this draft route.</summary>
    public StaticFieldV2CandidateKind CandidateKind { get; }

    /// <summary>Gets the count of leading expression segments interpreted as the owner name.</summary>
    public int OwnerSegmentCount { get; }

    /// <summary>Gets whether this partition derived exactly one, no, or several physical draft candidates.</summary>
    public StaticFieldV2TypeNameBindingResultKind ResultKind { get; }

    /// <summary>Gets a defensive copy of every physical draft group derived for this partition.</summary>
    public ImmutableArray<StaticFieldV2TypeNameCandidateGroup> Groups =>
        ExpressionV2ContractEncoding.Copy(groups);

    /// <summary>Gets the exact count of candidate occurrences examined for this partition.</summary>
    public int ExaminedOccurrenceCount { get; }

    /// <summary>Gets the retained typed rejection reason, or null when nothing was rejected.</summary>
    public StaticFieldV2TypeNameRejectionReason? FirstRejectionReason { get; }

    /// <summary>Gets a defensive copy of this fixed-reference canonical draft partition result.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft partition result.</summary>
    public string Sha256 { get; }

    /// <summary>Tests canonical equality between two draft partition results.</summary>
    /// <param name="other">The other draft partition result.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(StaticFieldV2TypeNamePartitionResult? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests draft partition-result equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a partition result with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldV2TypeNamePartitionResult);

    /// <summary>Computes a deterministic hash code from immutable canonical draft partition-result content.</summary>
    /// <returns>A hash code for this canonical draft partition result.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static StaticFieldV2TypeNamePartitionResult Create(
        object mintCapability,
        int partitionIndex,
        StaticFieldV2CandidateKind candidateKind,
        int ownerSegmentCount,
        StaticFieldV2TypeNameBindingResultKind resultKind,
        ImmutableArray<StaticFieldV2TypeNameCandidateGroup> groups,
        int examinedOccurrenceCount,
        StaticFieldV2TypeNameRejectionReason? firstRejectionReason)
    {
        if (!StaticFieldV2TypeNameBindingOutcome.OwnsRowMintCapability(mintCapability))
        {
            throw new ArgumentException(
                "A type-name partition result requires the binding outcome's private mint capability.",
                nameof(mintCapability));
        }

        ExpressionV2ContractEncoding.RequireDefined(candidateKind, nameof(candidateKind));
        ExpressionV2ContractEncoding.RequireDefined(resultKind, nameof(resultKind));
        ArgumentOutOfRangeException.ThrowIfNegative(partitionIndex);
        ArgumentOutOfRangeException.ThrowIfNegative(ownerSegmentCount);
        ArgumentOutOfRangeException.ThrowIfNegative(examinedOccurrenceCount);
        if (firstRejectionReason.HasValue)
        {
            ExpressionV2ContractEncoding.RequireDefined(firstRejectionReason.Value, nameof(firstRejectionReason));
        }
        var copied = ExpressionV2ContractEncoding.CopyRequired(
            groups,
            nameof(groups),
            StaticFieldV2Limits.MaximumGroupedPhysicalCandidateCount + 1);
        var expected = copied.Length switch
        {
            0 => StaticFieldV2TypeNameBindingResultKind.Absent,
            1 => StaticFieldV2TypeNameBindingResultKind.Exact,
            _ => StaticFieldV2TypeNameBindingResultKind.Ambiguous,
        };
        if (resultKind != expected)
        {
            throw new ArgumentException(
                "A partition result kind must be derived from its retained physical group count.",
                nameof(resultKind));
        }

        return new StaticFieldV2TypeNamePartitionResult(
            partitionIndex,
            candidateKind,
            ownerSegmentCount,
            resultKind,
            copied,
            examinedOccurrenceCount,
            firstRejectionReason);
    }
}

/// <summary>Freezes the complete draft outcome of explicit-route namespace/type name binding.</summary>
/// <remarks>
/// This sealed draft outcome is the sole issuer of every candidate occurrence, physical candidate group, and
/// partition result it retains. Binding is exhaustive: every qualified-owner partition is examined and every
/// namespace-versus-nested split of that partition's owner name is examined, so no first-match heuristic and no
/// longest-prefix preference can influence the answer.
/// <para>
/// A complete derived answer - <see cref="StaticFieldV2TypeNameBindingResultKind.Exact"/>,
/// <see cref="StaticFieldV2TypeNameBindingResultKind.Absent"/>, or
/// <see cref="StaticFieldV2TypeNameBindingResultKind.Ambiguous"/> - retains its partition results because those
/// results are the derivation itself rather than a truncated prefix. A stop -
/// <see cref="StaticFieldV2TypeNameBindingResultKind.NonExact"/>,
/// <see cref="StaticFieldV2TypeNameBindingResultKind.Invalid"/>, or
/// <see cref="StaticFieldV2TypeNameBindingResultKind.Unsupported"/> - is prefix-free and retains neither partition
/// results nor a selected candidate, because a partial derivation is not evidence about the complete name.
/// </para>
/// </remarks>
public sealed class StaticFieldV2TypeNameBindingOutcome : IEquatable<StaticFieldV2TypeNameBindingOutcome>
{
    private const string CanonicalDomain = "static-field-v2-type-name-binding-outcome";
    private const int CanonicalSchemaVersion = 1;
    private static readonly object RowMintCapability = new();
    private readonly ImmutableArray<StaticFieldV2TypeNamePartitionResult> partitionResults;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldV2TypeNameBindingOutcome(
        StaticFieldV2TypeNameBindingResultKind resultKind,
        StaticFieldV2TypeNameBindingIssue issue,
        StaticFieldV2ExpressionDescriptor expression,
        MetadataNamedTypeDefinitionChainPortfolioIdentity chainPortfolio,
        ImmutableArray<StaticFieldV2TypeNamePartitionResult> partitionResults,
        StaticFieldV2TypeNameCandidateGroup? selectedCandidate,
        EvaluationDeterministicBound? reachedBound,
        int observedCount)
    {
        ResultKind = resultKind;
        Issue = issue;
        Expression = expression;
        ChainPortfolio = chainPortfolio;
        this.partitionResults = ExpressionV2ContractEncoding.Copy(partitionResults);
        SelectedCandidate = selectedCandidate;
        ReachedBound = reachedBound;
        ObservedCount = observedCount;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32((int)resultKind);
        writer.WriteInt32((int)issue);
        writer.WriteSha256(expression.Sha256, nameof(expression));
        writer.WriteSha256(chainPortfolio.Sha256, nameof(chainPortfolio));
        writer.WriteInt32(partitionResults.Length);
        foreach (var partitionResult in partitionResults)
        {
            writer.WriteSha256(partitionResult.Sha256, nameof(partitionResults));
        }
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, selectedCandidate?.Sha256);
        ExpressionV2ContractEncoding.WriteOptionalBound(writer, reachedBound);
        writer.WriteInt32(observedCount);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the maximum examined namespace/type candidate-occurrence draft count.</summary>
    public const int MaximumCandidateOccurrenceCount = StaticFieldV2Limits.MaximumNameCandidateOccurrenceCount;

    /// <summary>Gets the maximum grouped physical-candidate draft count.</summary>
    public const int MaximumGroupedPhysicalCandidateCount =
        StaticFieldV2Limits.MaximumGroupedPhysicalCandidateCount;

    /// <summary>Gets whether this draft binding is exact, absent, ambiguous, non-exact, invalid, or unsupported.</summary>
    public StaticFieldV2TypeNameBindingResultKind ResultKind { get; }

    /// <summary>Gets the typed draft binding issue, or none for an exact outcome.</summary>
    public StaticFieldV2TypeNameBindingIssue Issue { get; }

    /// <summary>Gets the admitted detached V2 syntax draft descriptor that was bound.</summary>
    public StaticFieldV2ExpressionDescriptor Expression { get; }

    /// <summary>Gets the named-TypeDef-chain draft portfolio that supplied every examined chain.</summary>
    public MetadataNamedTypeDefinitionChainPortfolioIdentity ChainPortfolio { get; }

    /// <summary>Gets a defensive copy of the per-partition draft results, or an empty array for a stop.</summary>
    public ImmutableArray<StaticFieldV2TypeNamePartitionResult> PartitionResults =>
        ExpressionV2ContractEncoding.Copy(partitionResults);

    /// <summary>Gets the single converged physical draft candidate, or null for anything but an exact outcome.</summary>
    public StaticFieldV2TypeNameCandidateGroup? SelectedCandidate { get; }

    /// <summary>Gets the declared draft bound reached at cap plus one, otherwise null.</summary>
    public EvaluationDeterministicBound? ReachedBound { get; }

    /// <summary>Gets the examined-occurrence total, the propagated prerequisite count, or the cap-plus-one observation.</summary>
    public int ObservedCount { get; }

    /// <summary>Gets a defensive copy of the bounded fixed-reference canonical draft outcome.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft outcome.</summary>
    public string Sha256 { get; }

    /// <summary>Tests canonical equality between two draft name-binding outcomes.</summary>
    /// <param name="other">The other draft outcome.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(StaticFieldV2TypeNameBindingOutcome? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests draft name-binding outcome equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for an outcome with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldV2TypeNameBindingOutcome);

    /// <summary>Computes a deterministic hash code from immutable canonical draft outcome content.</summary>
    /// <returns>A hash code for this canonical draft outcome.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static bool OwnsRowMintCapability(object? capability) =>
        ReferenceEquals(capability, RowMintCapability);

    internal static StaticFieldV2TypeNameCandidateOccurrence IssueOccurrence(
        StaticFieldMetadataModuleIdentity sourceModule,
        MetadataNamedTypeDefinitionChainIdentity chain,
        int namespaceSegmentCount,
        int partitionIndex) =>
        StaticFieldV2TypeNameCandidateOccurrence.Create(
            RowMintCapability,
            sourceModule,
            chain,
            namespaceSegmentCount,
            partitionIndex);

    internal static StaticFieldV2TypeNameCandidateGroup IssueGroup(
        StaticFieldMetadataModuleIdentity sourceModule,
        MetadataTypeDefinitionAuthorityIdentity finalTypeDefinition,
        ImmutableArray<StaticFieldV2TypeNameCandidateOccurrence> occurrences) =>
        StaticFieldV2TypeNameCandidateGroup.Create(
            RowMintCapability,
            sourceModule,
            finalTypeDefinition,
            occurrences);

    internal static StaticFieldV2TypeNamePartitionResult IssuePartitionResult(
        int partitionIndex,
        StaticFieldV2CandidateKind candidateKind,
        int ownerSegmentCount,
        ImmutableArray<StaticFieldV2TypeNameCandidateGroup> groups,
        int examinedOccurrenceCount,
        StaticFieldV2TypeNameRejectionReason? firstRejectionReason) =>
        StaticFieldV2TypeNamePartitionResult.Create(
            RowMintCapability,
            partitionIndex,
            candidateKind,
            ownerSegmentCount,
            groups.Length switch
            {
                0 => StaticFieldV2TypeNameBindingResultKind.Absent,
                1 => StaticFieldV2TypeNameBindingResultKind.Exact,
                _ => StaticFieldV2TypeNameBindingResultKind.Ambiguous,
            },
            groups,
            examinedOccurrenceCount,
            firstRejectionReason);

    internal static StaticFieldV2TypeNameBindingOutcome IssueComplete(
        StaticFieldV2TypeNameBindingResultKind resultKind,
        StaticFieldV2TypeNameBindingIssue issue,
        StaticFieldV2ExpressionDescriptor expression,
        MetadataNamedTypeDefinitionChainPortfolioIdentity chainPortfolio,
        ImmutableArray<StaticFieldV2TypeNamePartitionResult> partitionResults,
        StaticFieldV2TypeNameCandidateGroup? selectedCandidate,
        int observedCount) =>
        new(
            resultKind,
            issue,
            expression,
            chainPortfolio,
            partitionResults,
            selectedCandidate,
            null,
            observedCount);

    internal static StaticFieldV2TypeNameBindingOutcome IssueStop(
        StaticFieldV2TypeNameBindingResultKind resultKind,
        StaticFieldV2TypeNameBindingIssue issue,
        StaticFieldV2ExpressionDescriptor expression,
        MetadataNamedTypeDefinitionChainPortfolioIdentity chainPortfolio,
        EvaluationDeterministicBound? reachedBound,
        int observedCount) =>
        new(
            resultKind,
            issue,
            expression,
            chainPortfolio,
            [],
            null,
            reachedBound,
            observedCount);
}

/// <summary>Binds the detached V2 owner name segments to authority-issued draft TypeDef heads.</summary>
/// <remarks>
/// This draft binder owns the explicit route only: an absent qualifier or the exact <c>global::</c> qualifier. A
/// named alias qualifier and a bare-member-only descriptor both stop with a typed unsupported disposition because
/// they require the separately owned scoped-context route. The binder performs name binding only; it constructs no
/// type argument, looks up no member, reads no runtime, and consults no context or PDB.
/// </remarks>
public static class StaticFieldV2TypeNameBinder
{
    /// <summary>Binds every qualified-owner partition of one admitted draft descriptor exhaustively.</summary>
    /// <param name="expression">The admitted detached V2 syntax draft descriptor.</param>
    /// <param name="chainPortfolio">The exact multi-module named-TypeDef-chain draft portfolio.</param>
    /// <remarks>
    /// Every qualified-owner partition contributes its own complete draft answer, and every namespace-versus-nested
    /// split of that partition's owner name is examined before any answer is formed. Partitions whose exact answers
    /// name the same physical module and TypeDef converge; partitions whose exact answers name different physical
    /// pairs are ambiguous rather than resolved by prefix length or partition order.
    /// </remarks>
    /// <returns>A sealed immutable draft outcome that is either one complete answer or one prefix-free typed stop.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="expression"/> or <paramref name="chainPortfolio"/> is null.</exception>
    public static StaticFieldV2TypeNameBindingOutcome BindExplicitRoute(
        StaticFieldV2ExpressionDescriptor expression,
        MetadataNamedTypeDefinitionChainPortfolioIdentity chainPortfolio)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(chainPortfolio);

        if (chainPortfolio.ResultKind == MetadataNamedTypeDefinitionChainPortfolioResultKind.NonExact)
        {
            return StaticFieldV2TypeNameBindingOutcome.IssueStop(
                StaticFieldV2TypeNameBindingResultKind.NonExact,
                StaticFieldV2TypeNameBindingIssue.ChainPortfolioNonExact,
                expression,
                chainPortfolio,
                chainPortfolio.ReachedBound,
                chainPortfolio.ObservedCount);
        }
        if (chainPortfolio.ResultKind == MetadataNamedTypeDefinitionChainPortfolioResultKind.Invalid)
        {
            return StaticFieldV2TypeNameBindingOutcome.IssueStop(
                StaticFieldV2TypeNameBindingResultKind.Invalid,
                StaticFieldV2TypeNameBindingIssue.ChainPortfolioInvalid,
                expression,
                chainPortfolio,
                chainPortfolio.ReachedBound,
                chainPortfolio.ObservedCount);
        }
        if (expression.AliasQualifier.Kind == StaticFieldV2AliasKind.Named)
        {
            return StaticFieldV2TypeNameBindingOutcome.IssueStop(
                StaticFieldV2TypeNameBindingResultKind.Unsupported,
                StaticFieldV2TypeNameBindingIssue.ContextRouteNotSupported,
                expression,
                chainPortfolio,
                null,
                0);
        }

        var partitions = expression.Partitions;
        var segments = expression.Segments;
        var qualifiedCount = 0;
        foreach (var partition in partitions)
        {
            if (partition.CandidateKind == StaticFieldV2CandidateKind.QualifiedOwner)
            {
                qualifiedCount++;
            }
        }
        if (qualifiedCount == 0)
        {
            return StaticFieldV2TypeNameBindingOutcome.IssueStop(
                StaticFieldV2TypeNameBindingResultKind.Unsupported,
                StaticFieldV2TypeNameBindingIssue.NoQualifiedOwnerPartition,
                expression,
                chainPortfolio,
                null,
                0);
        }

        var modules = BuildModuleViews(chainPortfolio);
        var results = ImmutableArray.CreateBuilder<StaticFieldV2TypeNamePartitionResult>(qualifiedCount);
        var groupKeys = new HashSet<(string ModuleSha256, int TypeDefinitionToken)>();
        var examinedTotal = 0;

        for (var partitionIndex = 0; partitionIndex < partitions.Length; partitionIndex++)
        {
            var partition = partitions[partitionIndex];
            if (partition.CandidateKind != StaticFieldV2CandidateKind.QualifiedOwner)
            {
                continue;
            }

            var ownerSegmentCount = partition.FieldSegmentIndex;
            var accepted = new List<StaticFieldV2TypeNameCandidateOccurrence>();
            var partitionExamined = 0;
            StaticFieldV2TypeNameRejectionReason? retainedReason = null;
            var retainedRank = 0;

            for (var namespaceSegmentCount = 0;
                 namespaceSegmentCount < ownerSegmentCount;
                 namespaceSegmentCount++)
            {
                if (namespaceSegmentCount > 0 && segments[namespaceSegmentCount - 1].Arity != 0)
                {
                    break;
                }

                var namespaceText = JoinNamespaceText(segments, namespaceSegmentCount);
                var expectedChainLength = ownerSegmentCount - namespaceSegmentCount;
                foreach (var module in modules)
                {
                    foreach (var chain in module.Chains)
                    {
                        examinedTotal++;
                        partitionExamined++;
                        if (examinedTotal > StaticFieldV2TypeNameBindingOutcome.MaximumCandidateOccurrenceCount)
                        {
                            return StaticFieldV2TypeNameBindingOutcome.IssueStop(
                                StaticFieldV2TypeNameBindingResultKind.NonExact,
                                StaticFieldV2TypeNameBindingIssue.CandidateOccurrenceBoundReached,
                                expression,
                                chainPortfolio,
                                new EvaluationDeterministicBound(
                                    ExpressionV2ContractLimits.NameCandidateOccurrenceCountBoundName,
                                    StaticFieldV2TypeNameBindingOutcome.MaximumCandidateOccurrenceCount),
                                StaticFieldV2TypeNameBindingOutcome.MaximumCandidateOccurrenceCount + 1);
                        }

                        var reason = Examine(
                            chain,
                            expectedChainLength,
                            namespaceText,
                            segments,
                            namespaceSegmentCount);
                        if (reason is null)
                        {
                            if (groupKeys.Add((module.SourceModule.Sha256, chain.FinalTypeDefinitionToken)) &&
                                groupKeys.Count >
                                    StaticFieldV2TypeNameBindingOutcome.MaximumGroupedPhysicalCandidateCount)
                            {
                                return StaticFieldV2TypeNameBindingOutcome.IssueStop(
                                    StaticFieldV2TypeNameBindingResultKind.NonExact,
                                    StaticFieldV2TypeNameBindingIssue.CandidateGroupBoundReached,
                                    expression,
                                    chainPortfolio,
                                    new EvaluationDeterministicBound(
                                        ExpressionV2ContractLimits.GroupedPhysicalCandidateCountBoundName,
                                        StaticFieldV2TypeNameBindingOutcome.MaximumGroupedPhysicalCandidateCount),
                                    StaticFieldV2TypeNameBindingOutcome.MaximumGroupedPhysicalCandidateCount + 1);
                            }

                            accepted.Add(StaticFieldV2TypeNameBindingOutcome.IssueOccurrence(
                                module.SourceModule,
                                chain,
                                namespaceSegmentCount,
                                partitionIndex));
                            continue;
                        }

                        var rank = ProgressRank(reason.Value);
                        if (rank > retainedRank)
                        {
                            retainedRank = rank;
                            retainedReason = reason;
                        }
                    }
                }
            }

            results.Add(StaticFieldV2TypeNameBindingOutcome.IssuePartitionResult(
                partitionIndex,
                partition.CandidateKind,
                ownerSegmentCount,
                GroupOccurrences(accepted),
                partitionExamined,
                retainedReason));
        }

        return Select(expression, chainPortfolio, results.ToImmutable(), examinedTotal);
    }

    private static StaticFieldV2TypeNameBindingOutcome Select(
        StaticFieldV2ExpressionDescriptor expression,
        MetadataNamedTypeDefinitionChainPortfolioIdentity chainPortfolio,
        ImmutableArray<StaticFieldV2TypeNamePartitionResult> results,
        int examinedTotal)
    {
        foreach (var result in results)
        {
            if (result.ResultKind == StaticFieldV2TypeNameBindingResultKind.Ambiguous)
            {
                return StaticFieldV2TypeNameBindingOutcome.IssueComplete(
                    StaticFieldV2TypeNameBindingResultKind.Ambiguous,
                    StaticFieldV2TypeNameBindingIssue.AmbiguousWithinPartition,
                    expression,
                    chainPortfolio,
                    results,
                    null,
                    examinedTotal);
            }
        }

        StaticFieldV2TypeNameCandidateGroup? selected = null;
        var distinctKeys = new HashSet<(string ModuleSha256, int TypeDefinitionToken)>();
        foreach (var result in results)
        {
            if (result.ResultKind != StaticFieldV2TypeNameBindingResultKind.Exact)
            {
                continue;
            }

            var group = result.Groups[0];
            distinctKeys.Add((group.SourceModule.Sha256, group.FinalTypeDefinitionToken));
            selected ??= group;
        }

        if (distinctKeys.Count == 0)
        {
            return StaticFieldV2TypeNameBindingOutcome.IssueComplete(
                StaticFieldV2TypeNameBindingResultKind.Absent,
                StaticFieldV2TypeNameBindingIssue.CandidateAbsent,
                expression,
                chainPortfolio,
                results,
                null,
                examinedTotal);
        }
        if (distinctKeys.Count > 1)
        {
            return StaticFieldV2TypeNameBindingOutcome.IssueComplete(
                StaticFieldV2TypeNameBindingResultKind.Ambiguous,
                StaticFieldV2TypeNameBindingIssue.AmbiguousAcrossPartitions,
                expression,
                chainPortfolio,
                results,
                null,
                examinedTotal);
        }

        return StaticFieldV2TypeNameBindingOutcome.IssueComplete(
            StaticFieldV2TypeNameBindingResultKind.Exact,
            StaticFieldV2TypeNameBindingIssue.None,
            expression,
            chainPortfolio,
            results,
            selected,
            examinedTotal);
    }

    private static ImmutableArray<StaticFieldV2TypeNameCandidateGroup> GroupOccurrences(
        List<StaticFieldV2TypeNameCandidateOccurrence> accepted)
    {
        if (accepted.Count == 0)
        {
            return [];
        }

        var order = new List<(string ModuleSha256, int TypeDefinitionToken)>();
        var buckets = new Dictionary<
            (string ModuleSha256, int TypeDefinitionToken),
            List<StaticFieldV2TypeNameCandidateOccurrence>>();
        foreach (var occurrence in accepted)
        {
            var key = (occurrence.SourceModule.Sha256, occurrence.FinalTypeDefinitionToken);
            if (!buckets.TryGetValue(key, out var bucket))
            {
                bucket = [];
                buckets.Add(key, bucket);
                order.Add(key);
            }
            bucket.Add(occurrence);
        }

        var groups = ImmutableArray.CreateBuilder<StaticFieldV2TypeNameCandidateGroup>(order.Count);
        foreach (var key in order)
        {
            var bucket = buckets[key];
            groups.Add(StaticFieldV2TypeNameBindingOutcome.IssueGroup(
                bucket[0].SourceModule,
                bucket[0].FinalTypeDefinition,
                [.. bucket]));
        }
        return groups.MoveToImmutable();
    }

    private static StaticFieldV2TypeNameRejectionReason? Examine(
        MetadataNamedTypeDefinitionChainIdentity chain,
        int expectedChainLength,
        string namespaceText,
        ImmutableArray<StaticFieldV2ExpressionNameSegment> segments,
        int namespaceSegmentCount)
    {
        var chainSegments = chain.Segments;
        if (chainSegments.Length != expectedChainLength)
        {
            return StaticFieldV2TypeNameRejectionReason.SegmentCountMismatch;
        }
        if (chain.IsModulePseudoType)
        {
            return StaticFieldV2TypeNameRejectionReason.ModulePseudoType;
        }
        if (!string.Equals(chainSegments[0].RawNamespaceName, namespaceText, StringComparison.Ordinal))
        {
            return StaticFieldV2TypeNameRejectionReason.NamespaceMismatch;
        }

        for (var index = 0; index < chainSegments.Length; index++)
        {
            var chainSegment = chainSegments[index];
            var syntaxSegment = segments[namespaceSegmentCount + index];
            if (chainSegment.RoslynProjection is not { } projection ||
                chainSegment.IntroducedGenericArity is not { } introducedArity)
            {
                return StaticFieldV2TypeNameRejectionReason.NonExactCompilerMapping;
            }
            if (!string.Equals(
                    projection.ProjectedSimpleName,
                    syntaxSegment.Identifier.DecodedText,
                    StringComparison.Ordinal))
            {
                return StaticFieldV2TypeNameRejectionReason.SimpleNameMismatch;
            }
            if (introducedArity != syntaxSegment.Arity)
            {
                return StaticFieldV2TypeNameRejectionReason.ArityMismatch;
            }
            if (!chainSegment.CanAppearInCSharpNamedType)
            {
                return StaticFieldV2TypeNameRejectionReason.NotCSharpAddressable;
            }
        }

        return null;
    }

    private static int ProgressRank(StaticFieldV2TypeNameRejectionReason reason) => reason switch
    {
        StaticFieldV2TypeNameRejectionReason.SegmentCountMismatch => 1,
        StaticFieldV2TypeNameRejectionReason.ModulePseudoType => 2,
        StaticFieldV2TypeNameRejectionReason.NamespaceMismatch => 3,
        StaticFieldV2TypeNameRejectionReason.NonExactCompilerMapping => 4,
        StaticFieldV2TypeNameRejectionReason.SimpleNameMismatch => 5,
        StaticFieldV2TypeNameRejectionReason.ArityMismatch => 6,
        _ => 7,
    };

    private static string JoinNamespaceText(
        ImmutableArray<StaticFieldV2ExpressionNameSegment> segments,
        int namespaceSegmentCount)
    {
        if (namespaceSegmentCount == 0)
        {
            return string.Empty;
        }
        if (namespaceSegmentCount == 1)
        {
            return segments[0].Identifier.DecodedText;
        }

        var parts = new string[namespaceSegmentCount];
        for (var index = 0; index < namespaceSegmentCount; index++)
        {
            parts[index] = segments[index].Identifier.DecodedText;
        }
        return string.Join('.', parts);
    }

    private static ImmutableArray<ModuleChainView> BuildModuleViews(
        MetadataNamedTypeDefinitionChainPortfolioIdentity chainPortfolio)
    {
        var entries = chainPortfolio.Entries;
        var views = ImmutableArray.CreateBuilder<ModuleChainView>(entries.Length);
        foreach (var entry in entries)
        {
            views.Add(new ModuleChainView(entry.SourceModule, entry.ChainCatalog.Chains));
        }
        return views.MoveToImmutable();
    }

    private sealed record ModuleChainView(
        StaticFieldMetadataModuleIdentity SourceModule,
        ImmutableArray<MetadataNamedTypeDefinitionChainIdentity> Chains);
}
