using System.Collections.Immutable;
using PhoenixInspect.Core.Abstractions;

namespace PhoenixInspect.Product.DumpQuery;

/// <summary>Classifies one exact constructed-assignability draft answer between two closed metadata types.</summary>
/// <remarks>
/// <see cref="Assignable"/> and <see cref="NotAssignable"/> are complete derived draft answers that retain their
/// traversed base-chain edges and per-position variance decisions. <see cref="Unprovable"/> is also a complete draft
/// answer: it states that a declared coverage boundary, and not a proven negative, prevented a positive result.
/// <see cref="NonExact"/> and <see cref="Invalid"/> are prefix-free stops that retain no edge and no decision.
/// </remarks>
public enum StaticFieldV2AssignabilityResultKind
{
    /// <summary>The source closed type is assignable to the target closed type under the exact W8 rule.</summary>
    Assignable = 1,

    /// <summary>The exact W8 rule proved that the source closed type is not assignable to the target.</summary>
    NotAssignable = 2,

    /// <summary>A declared coverage boundary prevented a positive proof; this is not a proven negative.</summary>
    Unprovable = 3,

    /// <summary>A prerequisite or a declared draft bound prevented any complete answer.</summary>
    NonExact = 4,

    /// <summary>Complete supplied draft evidence contradicted the ancestry portfolio or itself.</summary>
    Invalid = 5,
}

/// <summary>Identifies the deterministic draft rule that decided one constructed-assignability answer.</summary>
/// <remarks>
/// Every reason names exactly one rule of the frozen W8.1 differential, so a consumer can replay which clause - and
/// not merely which verdict - produced the retained draft answer.
/// </remarks>
public enum StaticFieldV2AssignabilityReason
{
    /// <summary>No reason applies.</summary>
    None = 0,

    /// <summary>The two closed types were canonically identical.</summary>
    Identity = 1,

    /// <summary>The target was the exact System.Object core-role root and the source was a reference type.</summary>
    ObjectRoot = 2,

    /// <summary>Both closed types named one definition and every argument position matched by its variance.</summary>
    SameDefinitionArgumentsMatched = 3,

    /// <summary>The target's non-generic definition was reached on the source's bounded base chain.</summary>
    BaseChainDefinitionReached = 4,

    /// <summary>Both array elements were reference types and the element assignability recursion admitted them.</summary>
    ArrayElementCovariance = 5,

    /// <summary>The array topologies agreed and their elements were canonically equal.</summary>
    ArrayElementEqual = 6,

    /// <summary>One complete bounded base chain never reached the target's definition.</summary>
    DefinitionNotOnBaseChain = 7,

    /// <summary>An invariant argument position held two arguments that were not canonically equal.</summary>
    InvariantArgumentMismatch = 8,

    /// <summary>A covariant argument position's source argument was not assignable to its target argument.</summary>
    CovariantArgumentNotAssignable = 9,

    /// <summary>A contravariant argument position's target argument was not assignable to its source argument.</summary>
    ContravariantArgumentNotAssignable = 10,

    /// <summary>A declared variant position held a value-type argument, which the CLI rule makes invariant.</summary>
    ValueArgumentPositionInvariant = 11,

    /// <summary>Two multidimensional arrays declared different ranks.</summary>
    ArrayRankMismatch = 12,

    /// <summary>One array was SZ and the other was multidimensional; W8 requires equal topology.</summary>
    ArrayTopologyMismatch = 13,

    /// <summary>At least one array element was a value type and the two elements were not canonically equal.</summary>
    ArrayValueElementMismatch = 14,

    /// <summary>Two nullable closed types were not canonically equal.</summary>
    NullableNotEqual = 15,

    /// <summary>Two primitive closed types were not canonically equal.</summary>
    PrimitiveNotEqual = 16,

    /// <summary>The source had value-type semantics, and this draft slice models no boxing conversion.</summary>
    ValueSourceRequiresBoxing = 17,

    /// <summary>The two closed topology kinds admit no assignability rule of this draft slice.</summary>
    ClosedTypeKindMismatch = 18,

    /// <summary>The target was an interface the class base chain cannot reach and no portfolio was supplied.</summary>
    InterfaceTargetNotModeled = 19,

    /// <summary>The target's generic definition was reached as a base, whose closed arguments are not modeled here.</summary>
    GenericBaseInstantiationNotModeled = 20,

    /// <summary>The bounded base chain stopped at a terminal that proves neither reach nor absence.</summary>
    AncestryChainIncomplete = 21,

    /// <summary>The ancestry authority portfolio prerequisite was non-exact.</summary>
    AncestryPortfolioNonExact = 22,

    /// <summary>The ancestry authority portfolio prerequisite was invalid.</summary>
    AncestryPortfolioInvalid = 23,

    /// <summary>The source's named definition was not issued by the exact ancestry portfolio.</summary>
    SourceDefinitionNotIssued = 24,

    /// <summary>The flattened argument vectors and the definition's GenericParam rows disagreed in count.</summary>
    ArgumentArityConflict = 25,

    /// <summary>One GenericParam row carried the reserved variance combination.</summary>
    VarianceFlagsInvalid = 26,

    /// <summary>The bounded base chain reached the declared ancestry-depth draft cap plus one.</summary>
    AncestryDepthBoundReached = 27,

    /// <summary>The cumulative comparison count reached the declared draft cap plus one.</summary>
    ComparisonBoundReached = 28,

    /// <summary>One retained InterfaceImpl closure edge named the target interface definition.</summary>
    InterfaceEdgeImplemented = 29,

    /// <summary>One complete bounded interface closure proved the target interface definition absent.</summary>
    InterfaceNotImplemented = 30,

    /// <summary>The bounded interface closure stopped at a terminal that proves neither reach nor absence.</summary>
    InterfaceClosureIncomplete = 31,

    /// <summary>A generic interface instantiation is undecoded, so its arguments cannot be checked here.</summary>
    GenericInterfaceInstantiationNotModeled = 32,

    /// <summary>The supplied interface-implementation authority portfolio prerequisite was non-exact.</summary>
    InterfaceImplementationPortfolioNonExact = 33,

    /// <summary>The supplied interface-implementation authority portfolio prerequisite was invalid.</summary>
    InterfaceImplementationPortfolioInvalid = 34,
}

/// <summary>Classifies the ECMA-335 declared variance of one GenericParam draft row.</summary>
/// <remarks>The draft classification is a pure decoding of the GenericParam variance mask and nothing else.</remarks>
public enum StaticFieldV2GenericParameterVariance
{
    /// <summary>The parameter declared no variance.</summary>
    Invariant = 1,

    /// <summary>The parameter declared covariance.</summary>
    Covariant = 2,

    /// <summary>The parameter declared contravariance.</summary>
    Contravariant = 3,
}

/// <summary>Identifies one declared coverage boundary retained by a constructed-assignability draft outcome.</summary>
/// <remarks>
/// Every boundary is an informational draft fact rather than an error. A boundary states what this pure-contract phase
/// deliberately does not model, so a consumer can never mistake a silent gap - or a deliberate divergence from the
/// pinned runtime - for a proven negative.
/// </remarks>
public enum StaticFieldV2AssignabilityCoverageBoundary
{
    /// <summary>No interface-implementation portfolio was supplied, so an interface target is never reached.</summary>
    InterfaceImplementationNotModeled = 1,

    /// <summary>No boxing conversion is modeled, so a value-type source only satisfies canonical identity.</summary>
    BoxingConversionNotModeled = 2,

    /// <summary>
    /// W8 deliberately diverges from the pinned runtime: the runtime reports a positive result when the target is a
    /// rank-one multidimensional array and the source is an SZ array with a compatible reference element. W8 requires
    /// equal rank and equal SZ-versus-multidimensional topology before recursive reference-element assignability, so
    /// both cross-topology directions are refused here.
    /// </summary>
    SzToRankOneMultidimensionalDivergenceFromRuntime = 3,

    /// <summary>A generic definition was reached as a base whose closed arguments no consulted authority carries.</summary>
    GenericBaseInstantiationNotModeled = 4,

    /// <summary>
    /// A generic interface instantiation was reached whose closed arguments no consulted authority decodes, so no
    /// per-position variance decision over that instantiation is possible in this draft slice.
    /// </summary>
    GenericInterfaceInstantiationNotModeled = 5,
}

/// <summary>Freezes one consulted bounded base-chain level of a constructed-assignability draft answer.</summary>
/// <remarks>
/// The edge is minted only by <see cref="StaticFieldV2AssignabilityOutcome"/>. Level zero is the source's own named
/// definition and every later level is one authority-derived immediate base edge, so provenance can establish that the
/// answer consulted every nearer level before any farther one.
/// </remarks>
public sealed class StaticFieldV2AssignabilityBaseChainEdgeIdentity :
    IEquatable<StaticFieldV2AssignabilityBaseChainEdgeIdentity>
{
    private const string CanonicalDomain = "static-field-v2-assignability-base-chain-edge";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldV2AssignabilityBaseChainEdgeIdentity(
        int levelIndex,
        StaticFieldMetadataModuleIdentity sourceModule,
        MetadataTypeDefinitionAuthorityIdentity typeDefinition,
        bool isTargetDefinition)
    {
        LevelIndex = levelIndex;
        SourceModule = sourceModule;
        TypeDefinition = typeDefinition;
        IsTargetDefinition = isTargetDefinition;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32(levelIndex);
        writer.WriteSha256(sourceModule.Sha256, nameof(sourceModule));
        writer.WriteSha256(typeDefinition.Sha256, nameof(typeDefinition));
        writer.WriteBoolean(isTargetDefinition);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the zero-based draft level index, where zero is the source's own named definition.</summary>
    public int LevelIndex { get; }

    /// <summary>Gets the exact metadata module that declares the TypeDef consulted at this draft level.</summary>
    public StaticFieldMetadataModuleIdentity SourceModule { get; }

    /// <summary>Gets the authority-issued TypeDef consulted at this bounded base-chain draft level.</summary>
    public MetadataTypeDefinitionAuthorityIdentity TypeDefinition { get; }

    /// <summary>Gets whether this draft level named the requested target's exact definition.</summary>
    public bool IsTargetDefinition { get; }

    /// <summary>Gets the exact TypeDef token consulted at this draft level.</summary>
    public int TypeDefinitionToken => TypeDefinition.TypeDefinitionToken;

    /// <summary>Gets a defensive copy of the fixed-reference canonical draft edge bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft edge.</summary>
    public string Sha256 { get; }

    /// <summary>Tests canonical equality between two consulted base-chain draft edges.</summary>
    /// <param name="other">The other draft edge.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(StaticFieldV2AssignabilityBaseChainEdgeIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests consulted base-chain draft edge equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for an edge with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldV2AssignabilityBaseChainEdgeIdentity);

    /// <summary>Computes a deterministic hash code from immutable canonical draft edge content.</summary>
    /// <returns>A hash code for this canonical draft edge.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static StaticFieldV2AssignabilityBaseChainEdgeIdentity Create(
        object mintCapability,
        int levelIndex,
        StaticFieldMetadataModuleIdentity sourceModule,
        MetadataTypeDefinitionAuthorityIdentity typeDefinition,
        bool isTargetDefinition)
    {
        if (!StaticFieldV2AssignabilityOutcome.OwnsRowMintCapability(mintCapability))
        {
            throw new ArgumentException(
                "A base-chain edge requires the assignability outcome's private mint capability.",
                nameof(mintCapability));
        }

        ArgumentNullException.ThrowIfNull(sourceModule);
        ArgumentNullException.ThrowIfNull(typeDefinition);
        ArgumentOutOfRangeException.ThrowIfNegative(levelIndex);
        return new StaticFieldV2AssignabilityBaseChainEdgeIdentity(
            levelIndex,
            sourceModule,
            typeDefinition,
            isTargetDefinition);
    }
}

/// <summary>Freezes one examined generic-argument position of a constructed-assignability draft answer.</summary>
/// <remarks>
/// The position is minted only by <see cref="StaticFieldV2AssignabilityOutcome"/>. It retains the physical GenericParam
/// row, the declared variance decoded from that row, the effective variance after the CLI value-type rule, both
/// examined closed arguments, and whether the position admitted the pair.
/// </remarks>
public sealed class StaticFieldV2AssignabilityVariancePositionIdentity :
    IEquatable<StaticFieldV2AssignabilityVariancePositionIdentity>
{
    private const string CanonicalDomain = "static-field-v2-assignability-variance-position";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldV2AssignabilityVariancePositionIdentity(
        int positionIndex,
        MetadataGenericParameterTableRowIdentity parameter,
        StaticFieldV2GenericParameterVariance declaredVariance,
        StaticFieldV2GenericParameterVariance effectiveVariance,
        MetadataClosedTypeIdentity sourceArgument,
        MetadataClosedTypeIdentity targetArgument,
        bool isAdmitted)
    {
        PositionIndex = positionIndex;
        Parameter = parameter;
        DeclaredVariance = declaredVariance;
        EffectiveVariance = effectiveVariance;
        SourceArgument = sourceArgument;
        TargetArgument = targetArgument;
        IsAdmitted = isAdmitted;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32(positionIndex);
        writer.WriteSha256(parameter.Sha256, nameof(parameter));
        writer.WriteInt32((int)declaredVariance);
        writer.WriteInt32((int)effectiveVariance);
        writer.WriteSha256(sourceArgument.Sha256, nameof(sourceArgument));
        writer.WriteSha256(targetArgument.Sha256, nameof(targetArgument));
        writer.WriteBoolean(isAdmitted);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the zero-based position in the canonical flattened draft argument vector.</summary>
    public int PositionIndex { get; }

    /// <summary>Gets the exact physical GenericParam draft row that governs this position.</summary>
    public MetadataGenericParameterTableRowIdentity Parameter { get; }

    /// <summary>Gets the variance decoded from the GenericParam draft row's variance mask.</summary>
    public StaticFieldV2GenericParameterVariance DeclaredVariance { get; }

    /// <summary>Gets the variance actually applied after the CLI value-type-argument rule.</summary>
    public StaticFieldV2GenericParameterVariance EffectiveVariance { get; }

    /// <summary>Gets the exact closed argument the source construction supplied at this draft position.</summary>
    public MetadataClosedTypeIdentity SourceArgument { get; }

    /// <summary>Gets the exact closed argument the target construction supplied at this draft position.</summary>
    public MetadataClosedTypeIdentity TargetArgument { get; }

    /// <summary>Gets whether this draft position admitted the examined argument pair.</summary>
    public bool IsAdmitted { get; }

    /// <summary>Gets the exact GenericParam token of the draft row that governs this position.</summary>
    public int GenericParameterToken => Parameter.GenericParameterToken;

    /// <summary>Gets a defensive copy of the fixed-reference canonical draft position bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft position.</summary>
    public string Sha256 { get; }

    /// <summary>Tests canonical equality between two examined variance draft positions.</summary>
    /// <param name="other">The other draft position.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(StaticFieldV2AssignabilityVariancePositionIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests examined variance draft position equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a position with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldV2AssignabilityVariancePositionIdentity);

    /// <summary>Computes a deterministic hash code from immutable canonical draft position content.</summary>
    /// <returns>A hash code for this canonical draft position.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static StaticFieldV2AssignabilityVariancePositionIdentity Create(
        object mintCapability,
        int positionIndex,
        MetadataGenericParameterTableRowIdentity parameter,
        StaticFieldV2GenericParameterVariance declaredVariance,
        StaticFieldV2GenericParameterVariance effectiveVariance,
        MetadataClosedTypeIdentity sourceArgument,
        MetadataClosedTypeIdentity targetArgument,
        bool isAdmitted)
    {
        if (!StaticFieldV2AssignabilityOutcome.OwnsRowMintCapability(mintCapability))
        {
            throw new ArgumentException(
                "A variance position requires the assignability outcome's private mint capability.",
                nameof(mintCapability));
        }

        ArgumentNullException.ThrowIfNull(parameter);
        ArgumentNullException.ThrowIfNull(sourceArgument);
        ArgumentNullException.ThrowIfNull(targetArgument);
        ArgumentOutOfRangeException.ThrowIfNegative(positionIndex);
        ExpressionV2ContractEncoding.RequireDefined(declaredVariance, nameof(declaredVariance));
        ExpressionV2ContractEncoding.RequireDefined(effectiveVariance, nameof(effectiveVariance));
        return new StaticFieldV2AssignabilityVariancePositionIdentity(
            positionIndex,
            parameter,
            declaredVariance,
            effectiveVariance,
            sourceArgument,
            targetArgument,
            isAdmitted);
    }
}

/// <summary>Freezes one complete constructed-assignability draft request.</summary>
/// <remarks>
/// The request names one source closed type, one target closed type, one exact ancestry draft portfolio, and an
/// optional interface-implementation draft portfolio. It carries no runtime, no dump, no memory, and no ClrMD
/// evidence: the decision is pure contract logic over already-landed metadata authority, taken before any suffix of
/// the expression is evaluated.
/// <para>
/// The interface-implementation portfolio is optional because it is a separately acquired authority. When it is
/// absent the decision keeps the declared
/// <see cref="StaticFieldV2AssignabilityCoverageBoundary.InterfaceImplementationNotModeled"/> boundary and an
/// interface target stays unprovable; when it is present that boundary is not declared and an interface target is
/// decided from the portfolio's bounded transitive closure.
/// </para>
/// </remarks>
public sealed class StaticFieldV2AssignabilityRequest : IEquatable<StaticFieldV2AssignabilityRequest>
{
    private const string CanonicalDomain = "static-field-v2-assignability-request";
    private const int CanonicalSchemaVersion = 2;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldV2AssignabilityRequest(
        MetadataClosedTypeIdentity sourceType,
        MetadataClosedTypeIdentity targetType,
        MetadataAncestryAuthorityPortfolioIdentity ancestryPortfolio,
        MetadataInterfaceImplementationPortfolioIdentity? interfaceImplementationPortfolio)
    {
        SourceType = sourceType;
        TargetType = targetType;
        AncestryPortfolio = ancestryPortfolio;
        InterfaceImplementationPortfolio = interfaceImplementationPortfolio;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteSha256(sourceType.Sha256, nameof(sourceType));
        writer.WriteSha256(targetType.Sha256, nameof(targetType));
        writer.WriteSha256(ancestryPortfolio.Sha256, nameof(ancestryPortfolio));
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, interfaceImplementationPortfolio?.Sha256);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact closed type of the non-null reference draft value being validated.</summary>
    public MetadataClosedTypeIdentity SourceType { get; }

    /// <summary>Gets the exact closed type the validated draft value must be assignable to.</summary>
    public MetadataClosedTypeIdentity TargetType { get; }

    /// <summary>Gets the ancestry authority draft portfolio supplying core roles and bounded base chains.</summary>
    public MetadataAncestryAuthorityPortfolioIdentity AncestryPortfolio { get; }

    /// <summary>Gets the optional interface-implementation draft portfolio, or null when none was supplied.</summary>
    public MetadataInterfaceImplementationPortfolioIdentity? InterfaceImplementationPortfolio { get; }

    /// <summary>Gets a defensive copy of the fixed-reference canonical draft request bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft request.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one complete constructed-assignability draft request.</summary>
    /// <param name="sourceType">The exact closed type of the validated non-null reference draft value.</param>
    /// <param name="targetType">The exact closed type the validated draft value must be assignable to.</param>
    /// <param name="ancestryPortfolio">The ancestry authority draft portfolio prerequisite.</param>
    /// <param name="interfaceImplementationPortfolio">
    /// The optional interface-implementation draft portfolio. Omitting it keeps the declared InterfaceImpl coverage
    /// boundary and leaves every interface target unprovable; supplying it makes an interface target provable.
    /// </param>
    /// <returns>A sealed immutable draft request.</returns>
    /// <exception cref="ArgumentNullException">A required reference argument is null.</exception>
    public static StaticFieldV2AssignabilityRequest Create(
        MetadataClosedTypeIdentity sourceType,
        MetadataClosedTypeIdentity targetType,
        MetadataAncestryAuthorityPortfolioIdentity ancestryPortfolio,
        MetadataInterfaceImplementationPortfolioIdentity? interfaceImplementationPortfolio = null)
    {
        ArgumentNullException.ThrowIfNull(sourceType);
        ArgumentNullException.ThrowIfNull(targetType);
        ArgumentNullException.ThrowIfNull(ancestryPortfolio);
        return new StaticFieldV2AssignabilityRequest(
            sourceType,
            targetType,
            ancestryPortfolio,
            interfaceImplementationPortfolio);
    }

    /// <summary>Tests canonical equality between two constructed-assignability draft requests.</summary>
    /// <param name="other">The other draft request.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(StaticFieldV2AssignabilityRequest? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests constructed-assignability draft request equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a request with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldV2AssignabilityRequest);

    /// <summary>Computes a deterministic hash code from immutable canonical draft request content.</summary>
    /// <returns>A hash code for this canonical draft request.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Freezes the complete draft outcome of one exact constructed-assignability decision.</summary>
/// <remarks>
/// This sealed draft outcome is the sole issuer of every base-chain edge and variance position it retains. A complete
/// derived answer - <see cref="StaticFieldV2AssignabilityResultKind.Assignable"/>,
/// <see cref="StaticFieldV2AssignabilityResultKind.NotAssignable"/>, or
/// <see cref="StaticFieldV2AssignabilityResultKind.Unprovable"/> - retains the edges and positions it consulted,
/// because those are the derivation itself. A stop - <see cref="StaticFieldV2AssignabilityResultKind.NonExact"/> or
/// <see cref="StaticFieldV2AssignabilityResultKind.Invalid"/> - is prefix-free and retains neither, because a partial
/// derivation is not evidence about the requested pair.
/// <para>
/// Declared coverage boundaries are informational draft facts retained by every outcome. When no
/// interface-implementation portfolio was supplied the InterfaceImpl evidence is not modeled, so an interface target
/// that the class base chain cannot reach is unprovable rather than negative; no boxing conversion is modeled, so a
/// value-typed source satisfies only canonical identity; and the deliberate W8.1 divergence from the pinned runtime
/// for an SZ source with a rank-one multidimensional target is retained as an explicit boundary rather than left
/// implicit.
/// </para>
/// <para>
/// An interface decision that consulted a supplied portfolio additionally retains the matched InterfaceImpl closure
/// edge and the closure's typed terminal, so a consumer can replay which physical row and which bounded traversal
/// produced the answer.
/// </para>
/// </remarks>
public sealed class StaticFieldV2AssignabilityOutcome : IEquatable<StaticFieldV2AssignabilityOutcome>
{
    /// <summary>Gets the shared bounded base-chain depth draft cap applied by one complete decision.</summary>
    public const int MaximumAncestryDepth = StaticFieldV2Limits.MaximumConstructedAncestryDepth;

    /// <summary>Gets the shared cumulative comparison draft cap applied by one complete decision.</summary>
    /// <remarks>
    /// The comparison total deliberately reuses the declared constructed-ancestry-depth bound name and value rather
    /// than introducing a second numeric declaration: both count edges of the same bounded metadata derivation.
    /// </remarks>
    public const int MaximumComparisonCount = StaticFieldV2Limits.MaximumConstructedAncestryDepth;

    private const string CanonicalDomain = "static-field-v2-assignability-outcome";
    private const int CanonicalSchemaVersion = 2;
    private static readonly object RowMintCapability = new();
    private readonly ImmutableArray<StaticFieldV2AssignabilityBaseChainEdgeIdentity> baseChainEdges;
    private readonly ImmutableArray<StaticFieldV2AssignabilityVariancePositionIdentity> variancePositions;
    private readonly ImmutableArray<StaticFieldV2AssignabilityCoverageBoundary> declaredCoverageBoundaries;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldV2AssignabilityOutcome(
        StaticFieldV2AssignabilityResultKind resultKind,
        StaticFieldV2AssignabilityReason reason,
        StaticFieldV2AssignabilityRequest request,
        ImmutableArray<StaticFieldV2AssignabilityBaseChainEdgeIdentity> baseChainEdges,
        ImmutableArray<StaticFieldV2AssignabilityVariancePositionIdentity> variancePositions,
        ImmutableArray<StaticFieldV2AssignabilityCoverageBoundary> declaredCoverageBoundaries,
        MetadataInterfaceImplementationEdgeAuthorityIdentity? interfaceEdge,
        MetadataInterfaceImplementationClosureTerminalKind? interfaceClosureTerminal,
        EvaluationDeterministicBound? reachedBound,
        int observedCount,
        int? relatedMetadataToken)
    {
        ResultKind = resultKind;
        Reason = reason;
        Request = request;
        this.baseChainEdges = ExpressionV2ContractEncoding.Copy(baseChainEdges);
        this.variancePositions = ExpressionV2ContractEncoding.Copy(variancePositions);
        this.declaredCoverageBoundaries = ExpressionV2ContractEncoding.Copy(declaredCoverageBoundaries);
        InterfaceEdge = interfaceEdge;
        InterfaceClosureTerminal = interfaceClosureTerminal;
        ReachedBound = reachedBound;
        ObservedCount = observedCount;
        RelatedMetadataToken = relatedMetadataToken;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32((int)resultKind);
        writer.WriteInt32((int)reason);
        writer.WriteSha256(request.Sha256, nameof(request));
        writer.WriteInt32(baseChainEdges.Length);
        foreach (var edge in baseChainEdges)
        {
            writer.WriteSha256(edge.Sha256, nameof(baseChainEdges));
        }
        writer.WriteInt32(variancePositions.Length);
        foreach (var position in variancePositions)
        {
            writer.WriteSha256(position.Sha256, nameof(variancePositions));
        }
        writer.WriteInt32(declaredCoverageBoundaries.Length);
        foreach (var boundary in declaredCoverageBoundaries)
        {
            writer.WriteInt32((int)boundary);
        }
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, interfaceEdge?.Sha256);
        ExpressionV2ContractEncoding.WriteOptionalEnum(writer, interfaceClosureTerminal);
        ExpressionV2ContractEncoding.WriteOptionalBound(writer, reachedBound);
        writer.WriteInt32(observedCount);
        ExpressionV2ContractEncoding.WriteOptionalInt32(writer, relatedMetadataToken);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets whether this draft decision is assignable, not assignable, unprovable, or a typed stop.</summary>
    public StaticFieldV2AssignabilityResultKind ResultKind { get; }

    /// <summary>Gets the deterministic draft rule that decided this outcome.</summary>
    public StaticFieldV2AssignabilityReason Reason { get; }

    /// <summary>Gets the complete draft request that produced this outcome.</summary>
    public StaticFieldV2AssignabilityRequest Request { get; }

    /// <summary>Gets a defensive most-derived-first copy of every consulted draft edge, empty for a stop.</summary>
    public ImmutableArray<StaticFieldV2AssignabilityBaseChainEdgeIdentity> BaseChainEdges =>
        ExpressionV2ContractEncoding.Copy(baseChainEdges);

    /// <summary>Gets a defensive examination-order copy of every examined variance draft position.</summary>
    public ImmutableArray<StaticFieldV2AssignabilityVariancePositionIdentity> VariancePositions =>
        ExpressionV2ContractEncoding.Copy(variancePositions);

    /// <summary>Gets a defensive ascending copy of every declared coverage boundary of this draft answer.</summary>
    public ImmutableArray<StaticFieldV2AssignabilityCoverageBoundary> DeclaredCoverageBoundaries =>
        ExpressionV2ContractEncoding.Copy(declaredCoverageBoundaries);

    /// <summary>Gets the retained InterfaceImpl closure draft edge that named the target, otherwise null.</summary>
    public MetadataInterfaceImplementationEdgeAuthorityIdentity? InterfaceEdge { get; }

    /// <summary>Gets the consulted bounded interface draft closure terminal, otherwise null.</summary>
    public MetadataInterfaceImplementationClosureTerminalKind? InterfaceClosureTerminal { get; }

    /// <summary>Gets the declared draft bound reached at cap plus one, otherwise null.</summary>
    public EvaluationDeterministicBound? ReachedBound { get; }

    /// <summary>
    /// Gets the cumulative comparison count of a complete draft answer, or the propagated prerequisite or
    /// cap-plus-one observation of a typed stop.
    /// </summary>
    public int ObservedCount { get; }

    /// <summary>Gets the metadata token related to this draft answer or stop, otherwise null.</summary>
    public int? RelatedMetadataToken { get; }

    /// <summary>Gets a defensive copy of the bounded fixed-reference canonical draft outcome bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft outcome.</summary>
    public string Sha256 { get; }

    /// <summary>Tests canonical equality between two constructed-assignability draft outcomes.</summary>
    /// <param name="other">The other draft outcome.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(StaticFieldV2AssignabilityOutcome? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests constructed-assignability draft outcome equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for an outcome with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldV2AssignabilityOutcome);

    /// <summary>Computes a deterministic hash code from immutable canonical draft outcome content.</summary>
    /// <returns>A hash code for this canonical draft outcome.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static bool OwnsRowMintCapability(object? capability) =>
        ReferenceEquals(capability, RowMintCapability);

    internal static StaticFieldV2AssignabilityBaseChainEdgeIdentity IssueEdge(
        int levelIndex,
        StaticFieldMetadataModuleIdentity sourceModule,
        MetadataTypeDefinitionAuthorityIdentity typeDefinition,
        bool isTargetDefinition) =>
        StaticFieldV2AssignabilityBaseChainEdgeIdentity.Create(
            RowMintCapability,
            levelIndex,
            sourceModule,
            typeDefinition,
            isTargetDefinition);

    internal static StaticFieldV2AssignabilityVariancePositionIdentity IssuePosition(
        int positionIndex,
        MetadataGenericParameterTableRowIdentity parameter,
        StaticFieldV2GenericParameterVariance declaredVariance,
        StaticFieldV2GenericParameterVariance effectiveVariance,
        MetadataClosedTypeIdentity sourceArgument,
        MetadataClosedTypeIdentity targetArgument,
        bool isAdmitted) =>
        StaticFieldV2AssignabilityVariancePositionIdentity.Create(
            RowMintCapability,
            positionIndex,
            parameter,
            declaredVariance,
            effectiveVariance,
            sourceArgument,
            targetArgument,
            isAdmitted);

    internal static StaticFieldV2AssignabilityOutcome IssueComplete(
        StaticFieldV2AssignabilityResultKind resultKind,
        StaticFieldV2AssignabilityReason reason,
        StaticFieldV2AssignabilityRequest request,
        ImmutableArray<StaticFieldV2AssignabilityBaseChainEdgeIdentity> baseChainEdges,
        ImmutableArray<StaticFieldV2AssignabilityVariancePositionIdentity> variancePositions,
        ImmutableArray<StaticFieldV2AssignabilityCoverageBoundary> declaredCoverageBoundaries,
        MetadataInterfaceImplementationEdgeAuthorityIdentity? interfaceEdge,
        MetadataInterfaceImplementationClosureTerminalKind? interfaceClosureTerminal,
        int observedCount,
        int? relatedMetadataToken) =>
        new(
            resultKind,
            reason,
            request,
            baseChainEdges,
            variancePositions,
            declaredCoverageBoundaries,
            interfaceEdge,
            interfaceClosureTerminal,
            null,
            observedCount,
            relatedMetadataToken);

    internal static StaticFieldV2AssignabilityOutcome IssueStop(
        StaticFieldV2AssignabilityResultKind resultKind,
        StaticFieldV2AssignabilityReason reason,
        StaticFieldV2AssignabilityRequest request,
        ImmutableArray<StaticFieldV2AssignabilityCoverageBoundary> declaredCoverageBoundaries,
        EvaluationDeterministicBound? reachedBound,
        int observedCount,
        int? relatedMetadataToken) =>
        new(
            resultKind,
            reason,
            request,
            [],
            [],
            declaredCoverageBoundaries,
            null,
            null,
            reachedBound,
            observedCount,
            relatedMetadataToken);
}

/// <summary>Decides exact constructed assignability between two closed types from metadata authority alone.</summary>
/// <remarks>
/// This draft binder owns the exact W8 assignability rule and nothing else. It consults only the already-landed closed
/// construction model and the ancestry authority portfolio: no runtime, dump, memory, or ClrMD capability is called,
/// and no physical table is re-read. The rules are evaluated in one fixed order - canonical identity, the System.Object
/// core-role root, same-definition variance, the bounded base chain, array topology, then nullable and primitive
/// equality - so a consumer can replay exactly which clause decided the retained answer.
/// <para>
/// An interface target is decided from the request's optional interface-implementation portfolio: a proved closure
/// edge admits, a complete closure that never names the target refuses, and every incomplete closure stays
/// unprovable. When no portfolio is supplied the InterfaceImpl evidence is not modeled, so an interface target that
/// the class base chain cannot reach remains unprovable rather than negative - the same honest boundary the
/// closed-construction binder takes for interface constraints. The remaining declared boundaries of this draft slice
/// are unchanged: no boxing conversion is modeled, so a value-typed source satisfies only canonical identity; the
/// closed arguments of a generic base definition reached on the chain are carried by no consulted authority; and a
/// generic interface instantiation is retained undecoded, so no variance decision over it is possible here.
/// </para>
/// <para>
/// One deliberate divergence from the pinned runtime is implemented here and is not inherited: the runtime reports a
/// positive result when the target is a rank-one multidimensional array and the source is an SZ array with a
/// compatible reference element. This draft rule requires equal rank <em>and</em> equal SZ-versus-multidimensional
/// topology before any recursive reference-element assignability, so both cross-topology directions are refused and
/// the divergence is retained as the
/// <see cref="StaticFieldV2AssignabilityCoverageBoundary.SzToRankOneMultidimensionalDivergenceFromRuntime"/> boundary.
/// Value elements never gain covariance in either topology.
/// </para>
/// </remarks>
public static class StaticFieldV2AssignabilityBinder
{
    private const int GenericParameterVarianceMask = 0x0003;
    private const int GenericParameterNonVariantFlag = 0x0000;
    private const int GenericParameterCovariantFlag = 0x0001;
    private const int GenericParameterContravariantFlag = 0x0002;

    /// <summary>Decides whether one closed source type is assignable to one closed target type.</summary>
    /// <param name="request">The complete constructed-assignability draft request.</param>
    /// <remarks>
    /// The decision is taken before any suffix of the expression is evaluated and validates a non-null reference draft
    /// value only. A negative answer is never inferred from a coverage boundary: every boundary produces
    /// <see cref="StaticFieldV2AssignabilityResultKind.Unprovable"/> and retains the evidence that reached it.
    /// </remarks>
    /// <returns>A sealed immutable draft outcome that is one complete answer or one prefix-free typed stop.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public static StaticFieldV2AssignabilityOutcome IsAssignable(StaticFieldV2AssignabilityRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var walk = new AssignabilityWalk(request);
        var portfolio = request.AncestryPortfolio;
        if (portfolio.ResultKind == MetadataAncestryAuthorityPortfolioResultKind.NonExact)
        {
            return Stop(
                walk,
                Decision.Stopped(
                    StaticFieldV2AssignabilityResultKind.NonExact,
                    StaticFieldV2AssignabilityReason.AncestryPortfolioNonExact,
                    null,
                    portfolio.ObservedCount,
                    null));
        }
        if (portfolio.ResultKind == MetadataAncestryAuthorityPortfolioResultKind.Invalid)
        {
            return Stop(
                walk,
                Decision.Stopped(
                    StaticFieldV2AssignabilityResultKind.Invalid,
                    StaticFieldV2AssignabilityReason.AncestryPortfolioInvalid,
                    null,
                    portfolio.ObservedCount,
                    null));
        }

        if (request.InterfaceImplementationPortfolio is { } interfacePortfolio &&
            interfacePortfolio.ResultKind != MetadataInterfaceImplementationPortfolioResultKind.Exact)
        {
            var invalid = interfacePortfolio.ResultKind ==
                MetadataInterfaceImplementationPortfolioResultKind.Invalid;
            return Stop(
                walk,
                Decision.Stopped(
                    invalid
                        ? StaticFieldV2AssignabilityResultKind.Invalid
                        : StaticFieldV2AssignabilityResultKind.NonExact,
                    invalid
                        ? StaticFieldV2AssignabilityReason.InterfaceImplementationPortfolioInvalid
                        : StaticFieldV2AssignabilityReason.InterfaceImplementationPortfolioNonExact,
                    interfacePortfolio.ReachedBound,
                    interfacePortfolio.ObservedCount,
                    null));
        }

        var decision = Decide(walk, request.SourceType, request.TargetType);
        if (decision.IsStop)
        {
            return Stop(walk, decision);
        }
        return StaticFieldV2AssignabilityOutcome.IssueComplete(
            decision.ResultKind,
            decision.Reason,
            request,
            walk.Edges.ToImmutable(),
            walk.Positions.ToImmutable(),
            walk.DeclaredBoundaries(),
            walk.InterfaceEdge,
            walk.InterfaceClosureTerminal,
            walk.ComparisonCount,
            decision.RelatedMetadataToken);
    }

    private static Decision Decide(
        AssignabilityWalk walk,
        MetadataClosedTypeIdentity source,
        MetadataClosedTypeIdentity target)
    {
        if (!walk.TryCountComparison())
        {
            return ComparisonBoundStop();
        }
        if (source.Equals(target))
        {
            return Decision.Answer(
                StaticFieldV2AssignabilityResultKind.Assignable,
                StaticFieldV2AssignabilityReason.Identity);
        }
        if (IsObjectRoot(walk, target))
        {
            return source.IsReferenceType
                ? Decision.Answer(
                    StaticFieldV2AssignabilityResultKind.Assignable,
                    StaticFieldV2AssignabilityReason.ObjectRoot)
                : Decision.Answer(
                    StaticFieldV2AssignabilityResultKind.NotAssignable,
                    StaticFieldV2AssignabilityReason.ValueSourceRequiresBoxing);
        }
        if (source.Kind == MetadataClosedTypeKind.Named && target.Kind == MetadataClosedTypeKind.Named)
        {
            return DecideNamed(walk, source, target);
        }
        if (source.Kind == MetadataClosedTypeKind.SzArray && target.Kind == MetadataClosedTypeKind.SzArray)
        {
            return DecideElements(walk, source, target);
        }
        if (source.Kind == MetadataClosedTypeKind.MultidimensionalArray &&
            target.Kind == MetadataClosedTypeKind.MultidimensionalArray)
        {
            return source.ArrayRank == target.ArrayRank
                ? DecideElements(walk, source, target)
                : Decision.Answer(
                    StaticFieldV2AssignabilityResultKind.NotAssignable,
                    StaticFieldV2AssignabilityReason.ArrayRankMismatch);
        }
        if (IsArray(source.Kind) && IsArray(target.Kind))
        {
            return Decision.Answer(
                StaticFieldV2AssignabilityResultKind.NotAssignable,
                StaticFieldV2AssignabilityReason.ArrayTopologyMismatch);
        }
        if (source.Kind == MetadataClosedTypeKind.Nullable && target.Kind == MetadataClosedTypeKind.Nullable)
        {
            return Decision.Answer(
                StaticFieldV2AssignabilityResultKind.NotAssignable,
                StaticFieldV2AssignabilityReason.NullableNotEqual);
        }
        if (source.Kind == MetadataClosedTypeKind.Primitive && target.Kind == MetadataClosedTypeKind.Primitive)
        {
            return Decision.Answer(
                StaticFieldV2AssignabilityResultKind.NotAssignable,
                StaticFieldV2AssignabilityReason.PrimitiveNotEqual);
        }
        return Decision.Answer(
            StaticFieldV2AssignabilityResultKind.NotAssignable,
            StaticFieldV2AssignabilityReason.ClosedTypeKindMismatch);
    }

    private static Decision DecideNamed(
        AssignabilityWalk walk,
        MetadataClosedTypeIdentity source,
        MetadataClosedTypeIdentity target)
    {
        var sourceClassification = source.FinalClassification!;
        var targetClassification = target.FinalClassification!;
        if (NamesSameDefinition(sourceClassification, targetClassification))
        {
            return DecideArguments(walk, source, target, targetClassification);
        }
        if (!source.IsReferenceType)
        {
            return Decision.Answer(
                StaticFieldV2AssignabilityResultKind.NotAssignable,
                StaticFieldV2AssignabilityReason.ValueSourceRequiresBoxing);
        }
        return WalkBaseChain(walk, sourceClassification, targetClassification);
    }

    private static Decision DecideArguments(
        AssignabilityWalk walk,
        MetadataClosedTypeIdentity source,
        MetadataClosedTypeIdentity target,
        MetadataTypeDefinitionSemanticClassificationIdentity definition)
    {
        var parameters = definition.TypeDefinition.GenericParameters;
        var sourceArguments = source.FlattenedArguments;
        var targetArguments = target.FlattenedArguments;
        if (sourceArguments.Length != targetArguments.Length || parameters.Length != targetArguments.Length)
        {
            return Decision.Stopped(
                StaticFieldV2AssignabilityResultKind.Invalid,
                StaticFieldV2AssignabilityReason.ArgumentArityConflict,
                null,
                targetArguments.Length,
                definition.TypeDefinition.TypeDefinitionToken);
        }

        for (var index = 0; index < parameters.Length; index++)
        {
            if (!walk.TryCountComparison())
            {
                return ComparisonBoundStop();
            }

            var parameter = parameters[index];
            if (DecodeVariance(parameter.Flags) is not { } declared)
            {
                return Decision.Stopped(
                    StaticFieldV2AssignabilityResultKind.Invalid,
                    StaticFieldV2AssignabilityReason.VarianceFlagsInvalid,
                    null,
                    parameter.Flags,
                    parameter.GenericParameterToken);
            }

            var sourceArgument = sourceArguments[index];
            var targetArgument = targetArguments[index];
            var valueArgument = !sourceArgument.IsReferenceType || !targetArgument.IsReferenceType;
            var effective = valueArgument ? StaticFieldV2GenericParameterVariance.Invariant : declared;
            var position = DecidePosition(walk, effective, sourceArgument, targetArgument, declared, valueArgument);
            walk.Positions.Add(StaticFieldV2AssignabilityOutcome.IssuePosition(
                index,
                parameter,
                declared,
                effective,
                sourceArgument,
                targetArgument,
                position.ResultKind == StaticFieldV2AssignabilityResultKind.Assignable));
            if (position.ResultKind != StaticFieldV2AssignabilityResultKind.Assignable)
            {
                return position.IsStop
                    ? position
                    : Decision.Answer(
                        position.ResultKind,
                        position.Reason,
                        parameter.GenericParameterToken);
            }
        }

        return Decision.Answer(
            StaticFieldV2AssignabilityResultKind.Assignable,
            StaticFieldV2AssignabilityReason.SameDefinitionArgumentsMatched,
            definition.TypeDefinition.TypeDefinitionToken);
    }

    private static Decision DecidePosition(
        AssignabilityWalk walk,
        StaticFieldV2GenericParameterVariance effective,
        MetadataClosedTypeIdentity sourceArgument,
        MetadataClosedTypeIdentity targetArgument,
        StaticFieldV2GenericParameterVariance declared,
        bool valueArgument)
    {
        if (effective == StaticFieldV2GenericParameterVariance.Invariant)
        {
            if (sourceArgument.Equals(targetArgument))
            {
                return Decision.Answer(
                    StaticFieldV2AssignabilityResultKind.Assignable,
                    StaticFieldV2AssignabilityReason.Identity);
            }
            return Decision.Answer(
                StaticFieldV2AssignabilityResultKind.NotAssignable,
                valueArgument && declared != StaticFieldV2GenericParameterVariance.Invariant
                    ? StaticFieldV2AssignabilityReason.ValueArgumentPositionInvariant
                    : StaticFieldV2AssignabilityReason.InvariantArgumentMismatch);
        }

        var covariant = effective == StaticFieldV2GenericParameterVariance.Covariant;
        var inner = covariant
            ? Decide(walk, sourceArgument, targetArgument)
            : Decide(walk, targetArgument, sourceArgument);
        if (inner.ResultKind != StaticFieldV2AssignabilityResultKind.NotAssignable)
        {
            return inner;
        }
        return Decision.Answer(
            StaticFieldV2AssignabilityResultKind.NotAssignable,
            covariant
                ? StaticFieldV2AssignabilityReason.CovariantArgumentNotAssignable
                : StaticFieldV2AssignabilityReason.ContravariantArgumentNotAssignable);
    }

    private static Decision DecideElements(
        AssignabilityWalk walk,
        MetadataClosedTypeIdentity source,
        MetadataClosedTypeIdentity target)
    {
        var sourceElement = source.ElementType!;
        var targetElement = target.ElementType!;
        if (!sourceElement.IsReferenceType || !targetElement.IsReferenceType)
        {
            return sourceElement.Equals(targetElement)
                ? Decision.Answer(
                    StaticFieldV2AssignabilityResultKind.Assignable,
                    StaticFieldV2AssignabilityReason.ArrayElementEqual)
                : Decision.Answer(
                    StaticFieldV2AssignabilityResultKind.NotAssignable,
                    StaticFieldV2AssignabilityReason.ArrayValueElementMismatch);
        }

        var element = Decide(walk, sourceElement, targetElement);
        return element.ResultKind == StaticFieldV2AssignabilityResultKind.Assignable
            ? Decision.Answer(
                StaticFieldV2AssignabilityResultKind.Assignable,
                StaticFieldV2AssignabilityReason.ArrayElementCovariance)
            : element;
    }

    private static Decision WalkBaseChain(
        AssignabilityWalk walk,
        MetadataTypeDefinitionSemanticClassificationIdentity source,
        MetadataTypeDefinitionSemanticClassificationIdentity target)
    {
        var chain = walk.Request.AncestryPortfolio.ExactAncestryChainOrDefault(
            source.SourceModule,
            source.TypeDefinition.TypeDefinitionToken);
        if (chain is null)
        {
            return Decision.Stopped(
                StaticFieldV2AssignabilityResultKind.Invalid,
                StaticFieldV2AssignabilityReason.SourceDefinitionNotIssued,
                null,
                0,
                source.TypeDefinition.TypeDefinitionToken);
        }

        var edges = chain.Edges;
        if (edges.Length > StaticFieldV2AssignabilityOutcome.MaximumAncestryDepth)
        {
            return AncestryDepthBoundStop(chain);
        }

        walk.Edges.Add(StaticFieldV2AssignabilityOutcome.IssueEdge(
            0,
            chain.SourceModule,
            chain.Subject,
            false));
        for (var level = 1; level <= edges.Length; level++)
        {
            var edge = edges[level - 1];
            var matched = edge.SourceModule.Equals(target.SourceModule) &&
                edge.Owner.Equals(target.TypeDefinition);
            walk.Edges.Add(StaticFieldV2AssignabilityOutcome.IssueEdge(
                level,
                edge.SourceModule,
                edge.Owner,
                matched));
            if (!matched)
            {
                continue;
            }
            if (target.TypeDefinition.TotalGenericArity == 0)
            {
                return Decision.Answer(
                    StaticFieldV2AssignabilityResultKind.Assignable,
                    StaticFieldV2AssignabilityReason.BaseChainDefinitionReached,
                    target.TypeDefinition.TypeDefinitionToken);
            }
            walk.DeclareGenericBaseBoundary();
            return Decision.Answer(
                StaticFieldV2AssignabilityResultKind.Unprovable,
                StaticFieldV2AssignabilityReason.GenericBaseInstantiationNotModeled,
                target.TypeDefinition.TypeDefinitionToken);
        }

        if (chain.TerminalKind == MetadataAncestryChainTerminalKind.DepthBoundReached)
        {
            return AncestryDepthBoundStop(chain);
        }
        if (target.Role == MetadataTypeDefinitionSemanticRole.Interface)
        {
            return DecideInterfaceTarget(walk, source, target);
        }
        if (chain.TerminalKind is MetadataAncestryChainTerminalKind.SystemObjectReached
            or MetadataAncestryChainTerminalKind.InterfaceRoot
            or MetadataAncestryChainTerminalKind.ModulePseudoTypeRoot)
        {
            return Decision.Answer(
                StaticFieldV2AssignabilityResultKind.NotAssignable,
                StaticFieldV2AssignabilityReason.DefinitionNotOnBaseChain,
                target.TypeDefinition.TypeDefinitionToken);
        }
        return Decision.Answer(
            StaticFieldV2AssignabilityResultKind.Unprovable,
            StaticFieldV2AssignabilityReason.AncestryChainIncomplete,
            target.TypeDefinition.TypeDefinitionToken);
    }

    private static Decision DecideInterfaceTarget(
        AssignabilityWalk walk,
        MetadataTypeDefinitionSemanticClassificationIdentity source,
        MetadataTypeDefinitionSemanticClassificationIdentity target)
    {
        var targetToken = target.TypeDefinition.TypeDefinitionToken;
        if (walk.Request.InterfaceImplementationPortfolio is not { } portfolio)
        {
            return Decision.Answer(
                StaticFieldV2AssignabilityResultKind.Unprovable,
                StaticFieldV2AssignabilityReason.InterfaceTargetNotModeled,
                targetToken);
        }

        var sourceModule = source.SourceModule;
        var sourceToken = source.TypeDefinition.TypeDefinitionToken;
        if (portfolio.ExactImplementedInterfacesOrDefault(sourceModule, sourceToken) is not { } closure)
        {
            return Decision.Answer(
                StaticFieldV2AssignabilityResultKind.Unprovable,
                StaticFieldV2AssignabilityReason.InterfaceClosureIncomplete,
                targetToken);
        }

        walk.RetainClosureTerminal(closure.TerminalKind);
        switch (portfolio.Implements(sourceModule, sourceToken, target.SourceModule, targetToken))
        {
            case MetadataInterfaceImplementationAnswer.Yes:
                if (MatchingEdge(closure, target) is not { } edge)
                {
                    return Decision.Answer(
                        StaticFieldV2AssignabilityResultKind.Unprovable,
                        StaticFieldV2AssignabilityReason.InterfaceClosureIncomplete,
                        targetToken);
                }
                walk.RetainInterfaceEdge(edge);
                if (target.TypeDefinition.TotalGenericArity > 0)
                {
                    walk.DeclareGenericInterfaceBoundary();
                    return Decision.Answer(
                        StaticFieldV2AssignabilityResultKind.Unprovable,
                        StaticFieldV2AssignabilityReason.GenericInterfaceInstantiationNotModeled,
                        targetToken);
                }
                return Decision.Answer(
                    StaticFieldV2AssignabilityResultKind.Assignable,
                    StaticFieldV2AssignabilityReason.InterfaceEdgeImplemented,
                    targetToken);
            case MetadataInterfaceImplementationAnswer.No:
                return Decision.Answer(
                    StaticFieldV2AssignabilityResultKind.NotAssignable,
                    StaticFieldV2AssignabilityReason.InterfaceNotImplemented,
                    targetToken);
            default:
                if (closure.TerminalKind ==
                    MetadataInterfaceImplementationClosureTerminalKind.GenericInterfaceReached)
                {
                    walk.DeclareGenericInterfaceBoundary();
                    return Decision.Answer(
                        StaticFieldV2AssignabilityResultKind.Unprovable,
                        StaticFieldV2AssignabilityReason.GenericInterfaceInstantiationNotModeled,
                        targetToken);
                }
                return Decision.Answer(
                    StaticFieldV2AssignabilityResultKind.Unprovable,
                    StaticFieldV2AssignabilityReason.InterfaceClosureIncomplete,
                    targetToken);
        }
    }

    private static MetadataInterfaceImplementationEdgeAuthorityIdentity? MatchingEdge(
        MetadataInterfaceImplementationClosureIdentity closure,
        MetadataTypeDefinitionSemanticClassificationIdentity target)
    {
        foreach (var edge in closure.InterfaceEdges)
        {
            if (edge.TargetModule is { } module &&
                edge.TargetTypeDefinition is { } definition &&
                module.Equals(target.SourceModule) &&
                definition.TypeDefinitionToken == target.TypeDefinition.TypeDefinitionToken)
            {
                return edge;
            }
        }
        return null;
    }

    private static bool NamesSameDefinition(
        MetadataTypeDefinitionSemanticClassificationIdentity left,
        MetadataTypeDefinitionSemanticClassificationIdentity right) =>
        left.SourceModule.Equals(right.SourceModule) && left.TypeDefinition.Equals(right.TypeDefinition);

    private static bool IsObjectRoot(AssignabilityWalk walk, MetadataClosedTypeIdentity target)
    {
        if (target.Kind == MetadataClosedTypeKind.Primitive)
        {
            return target.PrimitiveKind == MetadataPrimitiveTypeKind.Object;
        }
        if (target.Kind != MetadataClosedTypeKind.Named ||
            walk.Request.AncestryPortfolio.CoreRoles is not { } roles)
        {
            return false;
        }

        var classification = target.FinalClassification!;
        return classification.SourceModule.Equals(roles.CoreModule) &&
            classification.TypeDefinition.Equals(roles.SystemObject);
    }

    private static bool IsArray(MetadataClosedTypeKind kind) =>
        kind is MetadataClosedTypeKind.SzArray or MetadataClosedTypeKind.MultidimensionalArray;

    private static StaticFieldV2GenericParameterVariance? DecodeVariance(int flags) =>
        (flags & GenericParameterVarianceMask) switch
        {
            GenericParameterNonVariantFlag => StaticFieldV2GenericParameterVariance.Invariant,
            GenericParameterCovariantFlag => StaticFieldV2GenericParameterVariance.Covariant,
            GenericParameterContravariantFlag => StaticFieldV2GenericParameterVariance.Contravariant,
            _ => null,
        };

    private static Decision ComparisonBoundStop() =>
        Decision.Stopped(
            StaticFieldV2AssignabilityResultKind.NonExact,
            StaticFieldV2AssignabilityReason.ComparisonBoundReached,
            new EvaluationDeterministicBound(
                ExpressionV2ContractLimits.ConstructedAncestryDepthBoundName,
                StaticFieldV2AssignabilityOutcome.MaximumComparisonCount),
            StaticFieldV2AssignabilityOutcome.MaximumComparisonCount + 1,
            null);

    private static Decision AncestryDepthBoundStop(MetadataTypeDefinitionAncestryChainIdentity chain) =>
        Decision.Stopped(
            StaticFieldV2AssignabilityResultKind.NonExact,
            StaticFieldV2AssignabilityReason.AncestryDepthBoundReached,
            chain.ReachedBound ?? new EvaluationDeterministicBound(
                ExpressionV2ContractLimits.ConstructedAncestryDepthBoundName,
                StaticFieldV2AssignabilityOutcome.MaximumAncestryDepth),
            StaticFieldV2AssignabilityOutcome.MaximumAncestryDepth + 1,
            chain.RelatedMetadataToken);

    private static StaticFieldV2AssignabilityOutcome Stop(AssignabilityWalk walk, Decision decision) =>
        StaticFieldV2AssignabilityOutcome.IssueStop(
            decision.ResultKind,
            decision.Reason,
            walk.Request,
            walk.DeclaredBoundaries(),
            decision.ReachedBound,
            decision.ObservedCount,
            decision.RelatedMetadataToken);

    private sealed class AssignabilityWalk
    {
        private bool genericBaseBoundary;
        private bool genericInterfaceBoundary;

        internal AssignabilityWalk(StaticFieldV2AssignabilityRequest request) => Request = request;

        internal StaticFieldV2AssignabilityRequest Request { get; }

        internal MetadataInterfaceImplementationEdgeAuthorityIdentity? InterfaceEdge { get; private set; }

        internal MetadataInterfaceImplementationClosureTerminalKind? InterfaceClosureTerminal { get; private set; }

        internal ImmutableArray<StaticFieldV2AssignabilityBaseChainEdgeIdentity>.Builder Edges { get; } =
            ImmutableArray.CreateBuilder<StaticFieldV2AssignabilityBaseChainEdgeIdentity>();

        internal ImmutableArray<StaticFieldV2AssignabilityVariancePositionIdentity>.Builder Positions { get; } =
            ImmutableArray.CreateBuilder<StaticFieldV2AssignabilityVariancePositionIdentity>();

        internal int ComparisonCount { get; private set; }

        internal bool TryCountComparison() =>
            ++ComparisonCount <= StaticFieldV2AssignabilityOutcome.MaximumComparisonCount;

        internal void DeclareGenericBaseBoundary() => genericBaseBoundary = true;

        internal void DeclareGenericInterfaceBoundary() => genericInterfaceBoundary = true;

        internal void RetainInterfaceEdge(MetadataInterfaceImplementationEdgeAuthorityIdentity edge) =>
            InterfaceEdge ??= edge;

        internal void RetainClosureTerminal(MetadataInterfaceImplementationClosureTerminalKind terminalKind) =>
            InterfaceClosureTerminal ??= terminalKind;

        internal ImmutableArray<StaticFieldV2AssignabilityCoverageBoundary> DeclaredBoundaries()
        {
            var boundaries = ImmutableArray.CreateBuilder<StaticFieldV2AssignabilityCoverageBoundary>(5);
            if (Request.InterfaceImplementationPortfolio is null)
            {
                boundaries.Add(StaticFieldV2AssignabilityCoverageBoundary.InterfaceImplementationNotModeled);
            }
            boundaries.Add(StaticFieldV2AssignabilityCoverageBoundary.BoxingConversionNotModeled);
            boundaries.Add(
                StaticFieldV2AssignabilityCoverageBoundary.SzToRankOneMultidimensionalDivergenceFromRuntime);
            if (genericBaseBoundary)
            {
                boundaries.Add(StaticFieldV2AssignabilityCoverageBoundary.GenericBaseInstantiationNotModeled);
            }
            if (genericInterfaceBoundary)
            {
                boundaries.Add(
                    StaticFieldV2AssignabilityCoverageBoundary.GenericInterfaceInstantiationNotModeled);
            }
            return boundaries.ToImmutable();
        }
    }

    private sealed class Decision
    {
        private Decision(
            StaticFieldV2AssignabilityResultKind resultKind,
            StaticFieldV2AssignabilityReason reason,
            EvaluationDeterministicBound? reachedBound,
            int observedCount,
            int? relatedMetadataToken)
        {
            ResultKind = resultKind;
            Reason = reason;
            ReachedBound = reachedBound;
            ObservedCount = observedCount;
            RelatedMetadataToken = relatedMetadataToken;
        }

        internal StaticFieldV2AssignabilityResultKind ResultKind { get; }

        internal StaticFieldV2AssignabilityReason Reason { get; }

        internal EvaluationDeterministicBound? ReachedBound { get; }

        internal int ObservedCount { get; }

        internal int? RelatedMetadataToken { get; }

        internal bool IsStop => ResultKind is StaticFieldV2AssignabilityResultKind.NonExact
            or StaticFieldV2AssignabilityResultKind.Invalid;

        internal static Decision Answer(
            StaticFieldV2AssignabilityResultKind resultKind,
            StaticFieldV2AssignabilityReason reason,
            int? relatedMetadataToken = null) =>
            new(resultKind, reason, null, 0, relatedMetadataToken);

        internal static Decision Stopped(
            StaticFieldV2AssignabilityResultKind resultKind,
            StaticFieldV2AssignabilityReason reason,
            EvaluationDeterministicBound? reachedBound,
            int observedCount,
            int? relatedMetadataToken) =>
            new(resultKind, reason, reachedBound, observedCount, relatedMetadataToken);
    }
}
