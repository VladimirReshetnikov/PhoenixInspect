using System.Collections.Immutable;
using PhoenixInspect.Core.Abstractions;

namespace PhoenixInspect.Product.DumpQuery;

/// <summary>Classifies one closed-construction answer derived for a bound owner name.</summary>
/// <remarks>
/// <see cref="Exact"/> is the only complete derived answer; it alone retains the owner construction, the ordered
/// flattened argument vector, and the per-parameter constraint dispositions. <see cref="NonExact"/>,
/// <see cref="Invalid"/>, and <see cref="Unsupported"/> are prefix-free typed stops that retain no partial
/// construction.
/// </remarks>
public enum StaticFieldV2ClosedConstructionResultKind
{
    /// <summary>One exact closed metadata construction was derived and every constraint was proven satisfied.</summary>
    Exact = 1,

    /// <summary>A prerequisite, a declared bound, or an unprovable obligation prevented a complete answer.</summary>
    NonExact = 2,

    /// <summary>A complete prerequisite contradicted itself or a hard constraint was violated.</summary>
    Invalid = 3,

    /// <summary>The construction selects a later route that this owner-construction binder does not own.</summary>
    Unsupported = 4,
}

/// <summary>Identifies the deterministic issue for one closed-construction outcome.</summary>
/// <remarks>
/// This issue catalog keeps prerequisite, syntax-route, name-resolution, bound, and constraint answers
/// distinct so no consumer has to re-derive why a construction stopped.
/// </remarks>
public enum StaticFieldV2ClosedConstructionIssue
{
    /// <summary>No issue applies to an exact outcome.</summary>
    None = 0,

    /// <summary>The owner name binding was non-exact, invalid, or unsupported.</summary>
    NameBindingNotExact = 1,

    /// <summary>The owner name binding derived two or more distinct physical candidates.</summary>
    NameBindingAmbiguous = 2,

    /// <summary>The owner name binding derived no candidate at all.</summary>
    NameBindingAbsent = 3,

    /// <summary>The ancestry authority portfolio prerequisite was non-exact.</summary>
    AncestryPortfolioNonExact = 4,

    /// <summary>The ancestry authority portfolio prerequisite was invalid.</summary>
    AncestryPortfolioInvalid = 5,

    /// <summary>The constraint-target resolution portfolio prerequisite was non-exact.</summary>
    ConstraintPortfolioNonExact = 6,

    /// <summary>The constraint-target resolution portfolio prerequisite was invalid.</summary>
    ConstraintPortfolioInvalid = 7,

    /// <summary>The bound owner's metadata module is absent from one supplied exact portfolio.</summary>
    OwnerModuleNotInPortfolio = 8,

    /// <summary>One required TypeDef has no exact authority-issued semantic classification.</summary>
    DefinitionClassificationAbsent = 9,

    /// <summary>A per-segment or flattened generic arity disagreed with the authority-issued definition.</summary>
    ArityDisagreement = 10,

    /// <summary>An admitted predefined keyword has no closed metadata primitive counterpart.</summary>
    PredefinedTypeUnsupported = 11,

    /// <summary>A named type argument carries a named alias qualifier owned by the scoped-context route.</summary>
    TypeArgumentAliasUnsupported = 12,

    /// <summary>A named type argument matched no authority-issued chain.</summary>
    TypeArgumentAbsent = 13,

    /// <summary>A named type argument matched two or more distinct physical candidates.</summary>
    TypeArgumentAmbiguous = 14,

    /// <summary>A nullable construction was requested over an element that is not a non-nullable value type.</summary>
    NullableElementInvalid = 15,

    /// <summary>No exact <c>System.Nullable`1</c> definition exists in the exact core module.</summary>
    NullableDefinitionAbsent = 16,

    /// <summary>A multidimensional-array rank was below two or above the declared cap.</summary>
    ArrayRankInvalid = 17,

    /// <summary>The closed-type topology depth reached the declared cap plus one.</summary>
    TopologyDepthBoundReached = 18,

    /// <summary>The cumulative closed-type node count reached the declared cap plus one.</summary>
    TopologyNodeCountBoundReached = 19,

    /// <summary>The cumulative closed generic-argument count reached the declared cap plus one.</summary>
    ArgumentCountBoundReached = 20,

    /// <summary>The cumulative constraint-check count reached the declared cap plus one.</summary>
    ConstraintCheckBoundReached = 21,

    /// <summary>The examined default-constructor candidate count reached the declared cap plus one.</summary>
    DefaultConstructorSearchBoundReached = 22,

    /// <summary>One hard generic constraint was proven violated by its substituted argument.</summary>
    ConstraintViolated = 23,

    /// <summary>One generic constraint cannot be proven by the authorities this slice owns.</summary>
    ConstraintUnprovable = 24,

    /// <summary>The supplied interface-implementation authority portfolio prerequisite was non-exact.</summary>
    InterfaceImplementationPortfolioNonExact = 25,

    /// <summary>The supplied interface-implementation authority portfolio prerequisite was invalid.</summary>
    InterfaceImplementationPortfolioInvalid = 26,

    /// <summary>A generic contextual owner requires its decoded alias-target construction, which is not supplied.</summary>
    ContextualConstructionRequiresDecodedAliasTarget = 27,
}

/// <summary>Classifies the disposition of one substituted generic-constraint obligation.</summary>
/// <remarks>
/// <see cref="Satisfied"/> and <see cref="Violated"/> are proofs. <see cref="Unprovable"/> records that the obligation
/// is real but lies outside the authorities this slice consumes, which is never treated as a violation.
/// </remarks>
public enum StaticFieldV2ConstraintDisposition
{
    /// <summary>The substituted argument was proven to satisfy the obligation.</summary>
    Satisfied = 1,

    /// <summary>The substituted argument was proven to violate the obligation.</summary>
    Violated = 2,

    /// <summary>The obligation cannot be proven or refuted from the authorities this slice owns.</summary>
    Unprovable = 3,
}

/// <summary>Freezes one check of one substituted generic-parameter obligation.</summary>
/// <remarks>
/// The check is minted only by <see cref="StaticFieldV2ClosedConstructionOutcome"/>. It retains the authority-issued
/// GenericParam row, the exact substituted closed argument, the typed disposition, the optional constraint target that
/// created the obligation, and one optional stable reason name. An unconstrained parameter still produces one
/// satisfied check so every parameter has a disposition.
/// </remarks>
public sealed class StaticFieldV2ConstraintCheckIdentity : IEquatable<StaticFieldV2ConstraintCheckIdentity>
{
    private const string CanonicalDomain = "static-field-v2-constraint-check";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldV2ConstraintCheckIdentity(
        MetadataGenericParameterTableRowIdentity parameter,
        MetadataClosedTypeIdentity argument,
        StaticFieldV2ConstraintDisposition disposition,
        MetadataConstraintTargetResolutionIdentity? constraintTarget,
        string? reason)
    {
        Parameter = parameter;
        Argument = argument;
        Disposition = disposition;
        ConstraintTarget = constraintTarget;
        Reason = reason;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteSha256(parameter.Sha256, nameof(parameter));
        writer.WriteSha256(argument.Sha256, nameof(argument));
        writer.WriteInt32((int)disposition);
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, constraintTarget?.Sha256);
        ExpressionV2ContractEncoding.WriteOptionalString(writer, reason);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the authority-issued GenericParam row whose obligation was checked.</summary>
    public MetadataGenericParameterTableRowIdentity Parameter { get; }

    /// <summary>Gets the exact closed type substituted for the checked parameter.</summary>
    public MetadataClosedTypeIdentity Argument { get; }

    /// <summary>Gets the typed disposition proven for this obligation.</summary>
    public StaticFieldV2ConstraintDisposition Disposition { get; }

    /// <summary>Gets the constraint-target row that created this obligation, or null for a flag obligation.</summary>
    public MetadataConstraintTargetResolutionIdentity? ConstraintTarget { get; }

    /// <summary>Gets the stable lowercase reason name, or null when no reason applies.</summary>
    public string? Reason { get; }

    /// <summary>Gets the flattened zero-based parameter position within the owner construction.</summary>
    public int ParameterNumber => Parameter.Number;

    /// <summary>Gets a defensive copy of this fixed-reference canonical check.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical check.</summary>
    public string Sha256 { get; }

    /// <summary>Tests canonical equality between two constraint checks.</summary>
    /// <param name="other">The other check.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(StaticFieldV2ConstraintCheckIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests constraint-check equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a check with identical canonical content.</returns>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldV2ConstraintCheckIdentity);

    /// <summary>Computes a deterministic hash code from immutable canonical check content.</summary>
    /// <returns>A hash code for this canonical check.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static StaticFieldV2ConstraintCheckIdentity Create(
        object mintCapability,
        MetadataGenericParameterTableRowIdentity parameter,
        MetadataClosedTypeIdentity argument,
        StaticFieldV2ConstraintDisposition disposition,
        MetadataConstraintTargetResolutionIdentity? constraintTarget,
        string? reason)
    {
        if (!StaticFieldV2ClosedConstructionOutcome.OwnsRowMintCapability(mintCapability))
        {
            throw new ArgumentException(
                "A constraint check requires the closed-construction outcome's private mint capability.",
                nameof(mintCapability));
        }

        ArgumentNullException.ThrowIfNull(parameter);
        ArgumentNullException.ThrowIfNull(argument);
        ExpressionV2ContractEncoding.RequireDefined(disposition, nameof(disposition));
        if (reason is not null)
        {
            ExpressionV2ContractEncoding.RequireStableName(reason, nameof(reason));
        }

        return new StaticFieldV2ConstraintCheckIdentity(
            parameter,
            argument,
            disposition,
            constraintTarget,
            reason);
    }
}

/// <summary>Freezes the complete outcome of owner closed-construction binding.</summary>
/// <remarks>
/// This sealed outcome is the sole issuer of every constraint check it retains. It turns one exact bound owner
/// name plus that name's per-segment V2 type-argument syntax into one exact closed metadata construction, or into one
/// prefix-free typed stop. It looks up no member, reads no runtime storage, and consults no context or PDB.
/// <para>
/// An exact outcome retains the owner construction, the canonical outer-to-inner flattened argument vector, and one
/// satisfied check per generic-parameter obligation. A stop retains no construction and no argument vector. The
/// two constraint stops retain exactly the one decisive check that produced them, which is a complete typed fact
/// rather than a truncated derivation prefix.
/// </para>
/// </remarks>
public sealed class StaticFieldV2ClosedConstructionOutcome : IEquatable<StaticFieldV2ClosedConstructionOutcome>
{
    private const string CanonicalDomain = "static-field-v2-closed-construction-outcome";
    private const int CanonicalSchemaVersion = 3;
    private const int NameBindingEvidenceKind = 1;
    private const int ContextualBindingEvidenceKind = 2;
    private static readonly object RowMintCapability = new();
    private readonly ImmutableArray<MetadataClosedTypeIdentity> flattenedArguments;
    private readonly ImmutableArray<StaticFieldV2ConstraintCheckIdentity> constraintChecks;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldV2ClosedConstructionOutcome(
        StaticFieldV2ClosedConstructionResultKind resultKind,
        StaticFieldV2ClosedConstructionIssue issue,
        StaticFieldV2TypeNameBindingOutcome? nameBinding,
        StaticFieldV2ContextualBindingOutcome? contextualBinding,
        MetadataAncestryAuthorityPortfolioIdentity ancestryPortfolio,
        MetadataConstraintTargetResolutionPortfolioIdentity constraintPortfolio,
        MetadataInterfaceImplementationPortfolioIdentity? interfaceImplementationPortfolio,
        MetadataClosedTypeIdentity? ownerConstruction,
        ImmutableArray<MetadataClosedTypeIdentity> flattenedArguments,
        ImmutableArray<StaticFieldV2ConstraintCheckIdentity> constraintChecks,
        EvaluationDeterministicBound? reachedBound,
        int observedCount,
        int? relatedMetadataToken)
    {
        if ((nameBinding is null) == (contextualBinding is null))
        {
            throw new ArgumentException(
                "A closed-construction outcome carries exactly one binding evidence arm.",
                nameof(nameBinding));
        }

        ResultKind = resultKind;
        Issue = issue;
        NameBinding = nameBinding;
        ContextualBinding = contextualBinding;
        AncestryPortfolio = ancestryPortfolio;
        ConstraintPortfolio = constraintPortfolio;
        InterfaceImplementationPortfolio = interfaceImplementationPortfolio;
        OwnerConstruction = ownerConstruction;
        this.flattenedArguments = ExpressionV2ContractEncoding.Copy(flattenedArguments);
        this.constraintChecks = ExpressionV2ContractEncoding.Copy(constraintChecks);
        ReachedBound = reachedBound;
        ObservedCount = observedCount;
        RelatedMetadataToken = relatedMetadataToken;

        // Version 3 encodes the binding evidence as a tagged union, so a replay reader can always distinguish a
        // name-bound construction from a contextually bound one without inferring semantics from a bare digest.
        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32((int)resultKind);
        writer.WriteInt32((int)issue);
        writer.WriteInt32(nameBinding is not null ? NameBindingEvidenceKind : ContextualBindingEvidenceKind);
        writer.WriteSha256(
            nameBinding is not null ? nameBinding.Sha256 : contextualBinding!.Sha256,
            "bindingEvidence");
        writer.WriteSha256(ancestryPortfolio.Sha256, nameof(ancestryPortfolio));
        writer.WriteSha256(constraintPortfolio.Sha256, nameof(constraintPortfolio));
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, interfaceImplementationPortfolio?.Sha256);
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, ownerConstruction?.Sha256);
        writer.WriteInt32(flattenedArguments.Length);
        foreach (var argument in flattenedArguments)
        {
            writer.WriteSha256(argument.Sha256, nameof(flattenedArguments));
        }
        writer.WriteInt32(constraintChecks.Length);
        foreach (var check in constraintChecks)
        {
            writer.WriteSha256(check.Sha256, nameof(constraintChecks));
        }
        ExpressionV2ContractEncoding.WriteOptionalBound(writer, reachedBound);
        writer.WriteInt32(observedCount);
        ExpressionV2ContractEncoding.WriteOptionalInt32(writer, relatedMetadataToken);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the maximum closed-type topology depth admitted by this construction.</summary>
    public const int MaximumClosedTypeTopologyDepth = StaticFieldV2Limits.MaximumClosedTypeTopologyDepth;

    /// <summary>Gets the maximum cumulative closed-type node count admitted by this construction.</summary>
    public const int MaximumClosedTypeTopologyNodeCount = StaticFieldV2Limits.MaximumClosedTypeTopologyNodeCount;

    /// <summary>Gets the maximum cumulative closed generic-argument count admitted by this construction.</summary>
    public const int MaximumClosedArgumentCount = StaticFieldV2Limits.MaximumTypeSpecificationArgumentCount;

    /// <summary>Gets the maximum multidimensional-array rank admitted by this construction.</summary>
    public const int MaximumArrayRank = StaticFieldV2Limits.MaximumArrayRank;

    /// <summary>Gets the maximum constraint-check count retained by this construction.</summary>
    public const int MaximumConstraintCheckCount = StaticFieldV2Limits.MaximumGenericConstraintCount;

    /// <summary>Gets the maximum default-constructor candidate count examined for one obligation.</summary>
    public const int MaximumDefaultConstructorSearchCount =
        StaticFieldV2Limits.MaximumDefaultConstructorSearchCount;

    /// <summary>Gets whether this construction is exact, non-exact, invalid, or unsupported.</summary>
    public StaticFieldV2ClosedConstructionResultKind ResultKind { get; }

    /// <summary>Gets the typed construction issue, or none for an exact outcome.</summary>
    public StaticFieldV2ClosedConstructionIssue Issue { get; }

    /// <summary>Gets the retained owner name-binding outcome, or null for a contextually bound construction.</summary>
    /// <remarks>
    /// Exactly one binding-evidence arm is present: an explicit-route construction retains the name binding that
    /// supplied the owner head, and a contextual-route construction retains the contextual binding instead. The
    /// canonical bytes tag which arm produced the construction, so replay provenance never has to infer it.
    /// </remarks>
    public StaticFieldV2TypeNameBindingOutcome? NameBinding { get; }

    /// <summary>Gets the retained contextual binding outcome, or null for a name-bound construction.</summary>
    public StaticFieldV2ContextualBindingOutcome? ContextualBinding { get; }

    /// <summary>Gets the retained ancestry authority portfolio prerequisite.</summary>
    public MetadataAncestryAuthorityPortfolioIdentity AncestryPortfolio { get; }

    /// <summary>Gets the retained constraint-target resolution portfolio prerequisite.</summary>
    public MetadataConstraintTargetResolutionPortfolioIdentity ConstraintPortfolio { get; }

    /// <summary>Gets the retained optional interface-implementation portfolio, otherwise null.</summary>
    public MetadataInterfaceImplementationPortfolioIdentity? InterfaceImplementationPortfolio { get; }

    /// <summary>Gets the exact closed owner construction, or null for any typed stop.</summary>
    public MetadataClosedTypeIdentity? OwnerConstruction { get; }

    /// <summary>Gets a defensive copy of the canonical outer-to-inner flattened owner argument vector.</summary>
    public ImmutableArray<MetadataClosedTypeIdentity> FlattenedArguments =>
        ExpressionV2ContractEncoding.Copy(flattenedArguments);

    /// <summary>Gets a defensive copy of the retained per-obligation constraint checks.</summary>
    public ImmutableArray<StaticFieldV2ConstraintCheckIdentity> ConstraintChecks =>
        ExpressionV2ContractEncoding.Copy(constraintChecks);

    /// <summary>Gets the exact metadata module of the bound owner, or null for a stop before owner selection.</summary>
    public StaticFieldMetadataModuleIdentity? SourceModule =>
        NameBinding?.SelectedCandidate?.SourceModule ?? ContextualBinding?.SelectedCandidate?.SourceModule;

    /// <summary>Gets the declared bound reached at cap plus one, otherwise null.</summary>
    public EvaluationDeterministicBound? ReachedBound { get; }

    /// <summary>Gets the propagated prerequisite count or the cap-plus-one observation.</summary>
    public int ObservedCount { get; }

    /// <summary>Gets the issue-related metadata token, otherwise null.</summary>
    public int? RelatedMetadataToken { get; }

    /// <summary>Gets a defensive copy of the bounded fixed-reference canonical outcome.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical outcome.</summary>
    public string Sha256 { get; }

    /// <summary>Tests canonical equality between two closed-construction outcomes.</summary>
    /// <param name="other">The other outcome.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(StaticFieldV2ClosedConstructionOutcome? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests closed-construction outcome equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for an outcome with identical canonical content.</returns>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldV2ClosedConstructionOutcome);

    /// <summary>Computes a deterministic hash code from immutable canonical outcome content.</summary>
    /// <returns>A hash code for this canonical outcome.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static bool OwnsRowMintCapability(object? capability) =>
        ReferenceEquals(capability, RowMintCapability);

    internal static StaticFieldV2ConstraintCheckIdentity IssueCheck(
        MetadataGenericParameterTableRowIdentity parameter,
        MetadataClosedTypeIdentity argument,
        StaticFieldV2ConstraintDisposition disposition,
        MetadataConstraintTargetResolutionIdentity? constraintTarget,
        string? reason) =>
        StaticFieldV2ConstraintCheckIdentity.Create(
            RowMintCapability,
            parameter,
            argument,
            disposition,
            constraintTarget,
            reason);

    internal static StaticFieldV2ClosedConstructionOutcome IssueExact(
        StaticFieldV2TypeNameBindingOutcome? nameBinding,
        MetadataAncestryAuthorityPortfolioIdentity ancestryPortfolio,
        MetadataConstraintTargetResolutionPortfolioIdentity constraintPortfolio,
        MetadataInterfaceImplementationPortfolioIdentity? interfaceImplementationPortfolio,
        MetadataClosedTypeIdentity ownerConstruction,
        ImmutableArray<MetadataClosedTypeIdentity> flattenedArguments,
        ImmutableArray<StaticFieldV2ConstraintCheckIdentity> constraintChecks,
        int observedCount,
        int relatedMetadataToken,
        StaticFieldV2ContextualBindingOutcome? contextualBinding = null) =>
        new(
            StaticFieldV2ClosedConstructionResultKind.Exact,
            StaticFieldV2ClosedConstructionIssue.None,
            nameBinding,
            contextualBinding,
            ancestryPortfolio,
            constraintPortfolio,
            interfaceImplementationPortfolio,
            ownerConstruction,
            flattenedArguments,
            constraintChecks,
            null,
            observedCount,
            relatedMetadataToken);

    internal static StaticFieldV2ClosedConstructionOutcome IssueStop(
        StaticFieldV2ClosedConstructionResultKind resultKind,
        StaticFieldV2ClosedConstructionIssue issue,
        StaticFieldV2TypeNameBindingOutcome? nameBinding,
        MetadataAncestryAuthorityPortfolioIdentity ancestryPortfolio,
        MetadataConstraintTargetResolutionPortfolioIdentity constraintPortfolio,
        MetadataInterfaceImplementationPortfolioIdentity? interfaceImplementationPortfolio,
        EvaluationDeterministicBound? reachedBound,
        int observedCount,
        int? relatedMetadataToken,
        StaticFieldV2ConstraintCheckIdentity? decisiveCheck = null,
        StaticFieldV2ContextualBindingOutcome? contextualBinding = null) =>
        new(
            resultKind,
            issue,
            nameBinding,
            contextualBinding,
            ancestryPortfolio,
            constraintPortfolio,
            interfaceImplementationPortfolio,
            null,
            [],
            decisiveCheck is null
                ? ImmutableArray<StaticFieldV2ConstraintCheckIdentity>.Empty
                : ImmutableArray.Create(decisiveCheck),
            reachedBound,
            observedCount,
            relatedMetadataToken);
}

/// <summary>Turns one bound owner name plus its type-argument syntax into an exact closed construction.</summary>
/// <remarks>
/// This binder owns exactly one step: closed construction and substituted generic-constraint validation. It
/// consumes one exact name binding, one exact ancestry authority portfolio, and one exact constraint-target
/// resolution portfolio, and produces either one exact closed owner construction or one prefix-free typed stop.
/// <para>
/// An interface constraint target is decided from the optional interface-implementation portfolio: a proved bounded
/// transitive closure satisfies, a complete closure that never names the target violates, and every incomplete
/// closure stays unprovable with its typed terminal named in the retained reason. When no portfolio is supplied the
/// obligation remains deferred and unprovable, exactly as before. A substituted TypeSpec constraint target is
/// likewise deferred to the constructed-constraint substitution work. Neither deferral is ever reported as a
/// violation.
/// </para>
/// </remarks>
public static class StaticFieldV2ClosedConstructionBinder
{
    private const int ReferenceTypeConstraintFlag = 0x0004;
    private const int NotNullableValueTypeConstraintFlag = 0x0008;
    private const int DefaultConstructorConstraintFlag = 0x0010;
    private const int MethodMemberAccessMask = 0x0007;
    private const int MethodPublicAccess = 0x0006;
    private const string ConstructorName = ".ctor";
    private const string NullableNamespaceName = "System";
    private const string NullableMetadataName = "Nullable`1";

    private const string ReferenceTypeSatisfiedReason = "constraint.reference-type-satisfied";
    private const string ReferenceTypeViolatedReason = "constraint.reference-type-required";
    private const string ValueTypeSatisfiedReason = "constraint.value-type-satisfied";
    private const string ValueTypeViolatedReason = "constraint.value-type-required";
    private const string DefaultConstructorProvenReason = "constraint.default-constructor-proven";
    private const string DefaultConstructorUnprovenReason = "constraint.default-constructor-unproven";
    private const string BaseDefinitionExactReason = "constraint.base-definition-exact";
    private const string BaseDefinitionReachedReason = "constraint.base-definition-reached";
    private const string BaseDefinitionUnreachableReason = "constraint.base-definition-unreachable";
    private const string BaseDefinitionAncestryIncompleteReason = "constraint.base-definition-ancestry-incomplete";
    private const string InterfaceTargetReason = "constraint.interface-target-deferred";
    private const string InterfaceImplementedReason = "constraint.interface-implemented";
    private const string InterfaceNotImplementedReason = "constraint.interface-not-implemented";
    private const string InterfaceClosureReasonPrefix = "constraint.interface-closure-";
    private const string InterfaceClosureUnavailableReason = "constraint.interface-closure-unavailable";
    private const string TypeSpecificationTargetReason = "constraint.typespec-target-deferred";
    private const string UnresolvedReferenceTargetReason = "constraint.unresolved-reference-target";
    private const string TargetClassificationAbsentReason = "constraint.target-classification-absent";
    private const string NonNamedArgumentReason = "constraint.non-named-argument-deferred";

    /// <summary>Binds one exact owner name and its type-argument syntax to an exact closed construction.</summary>
    /// <param name="nameBinding">The owner name-binding outcome produced by the preceding slice.</param>
    /// <param name="ancestryPortfolio">The exact ancestry authority portfolio for every examined module.</param>
    /// <param name="constraintPortfolio">The exact constraint-target resolution portfolio.</param>
    /// <param name="interfaceImplementationPortfolio">
    /// The optional interface-implementation portfolio. Omitting it leaves every interface constraint target
    /// deferred and unprovable; supplying it decides the obligation from the argument's bounded transitive interface
    /// closure and never reports an incomplete closure as a violation.
    /// </param>
    /// <remarks>
    /// Every prerequisite is checked before any construction begins, every declared topology bound is checked
    /// before a construction factory is called so a crossing yields cap-plus-one evidence rather than an exception,
    /// and every constraint obligation of every chain segment is examined before an answer is formed.
    /// </remarks>
    /// <returns>A sealed immutable outcome that is one exact construction or one prefix-free typed stop.</returns>
    /// <exception cref="ArgumentNullException">Any required supplied argument is null.</exception>
    public static StaticFieldV2ClosedConstructionOutcome BindOwnerConstruction(
        StaticFieldV2TypeNameBindingOutcome nameBinding,
        MetadataAncestryAuthorityPortfolioIdentity ancestryPortfolio,
        MetadataConstraintTargetResolutionPortfolioIdentity constraintPortfolio,
        MetadataInterfaceImplementationPortfolioIdentity? interfaceImplementationPortfolio = null)
    {
        ArgumentNullException.ThrowIfNull(nameBinding);
        ArgumentNullException.ThrowIfNull(ancestryPortfolio);
        ArgumentNullException.ThrowIfNull(constraintPortfolio);

        if (nameBinding.ResultKind != StaticFieldV2TypeNameBindingResultKind.Exact)
        {
            var (resultKind, issue) = nameBinding.ResultKind switch
            {
                StaticFieldV2TypeNameBindingResultKind.Absent =>
                    (StaticFieldV2ClosedConstructionResultKind.NonExact,
                     StaticFieldV2ClosedConstructionIssue.NameBindingAbsent),
                StaticFieldV2TypeNameBindingResultKind.Ambiguous =>
                    (StaticFieldV2ClosedConstructionResultKind.Invalid,
                     StaticFieldV2ClosedConstructionIssue.NameBindingAmbiguous),
                StaticFieldV2TypeNameBindingResultKind.Invalid =>
                    (StaticFieldV2ClosedConstructionResultKind.Invalid,
                     StaticFieldV2ClosedConstructionIssue.NameBindingNotExact),
                StaticFieldV2TypeNameBindingResultKind.Unsupported =>
                    (StaticFieldV2ClosedConstructionResultKind.Unsupported,
                     StaticFieldV2ClosedConstructionIssue.NameBindingNotExact),
                _ =>
                    (StaticFieldV2ClosedConstructionResultKind.NonExact,
                     StaticFieldV2ClosedConstructionIssue.NameBindingNotExact),
            };
            return StaticFieldV2ClosedConstructionOutcome.IssueStop(
                resultKind,
                issue,
                nameBinding,
                ancestryPortfolio,
                constraintPortfolio,
                interfaceImplementationPortfolio,
                nameBinding.ReachedBound,
                nameBinding.ObservedCount,
                null);
        }

        if (ancestryPortfolio.ResultKind != MetadataAncestryAuthorityPortfolioResultKind.Exact)
        {
            var isInvalid = ancestryPortfolio.ResultKind == MetadataAncestryAuthorityPortfolioResultKind.Invalid;
            return StaticFieldV2ClosedConstructionOutcome.IssueStop(
                isInvalid
                    ? StaticFieldV2ClosedConstructionResultKind.Invalid
                    : StaticFieldV2ClosedConstructionResultKind.NonExact,
                isInvalid
                    ? StaticFieldV2ClosedConstructionIssue.AncestryPortfolioInvalid
                    : StaticFieldV2ClosedConstructionIssue.AncestryPortfolioNonExact,
                nameBinding,
                ancestryPortfolio,
                constraintPortfolio,
                interfaceImplementationPortfolio,
                null,
                ancestryPortfolio.ObservedCount,
                null);
        }

        if (constraintPortfolio.ResultKind != MetadataConstraintTargetResolutionPortfolioResultKind.Exact)
        {
            var isInvalid =
                constraintPortfolio.ResultKind == MetadataConstraintTargetResolutionPortfolioResultKind.Invalid;
            return StaticFieldV2ClosedConstructionOutcome.IssueStop(
                isInvalid
                    ? StaticFieldV2ClosedConstructionResultKind.Invalid
                    : StaticFieldV2ClosedConstructionResultKind.NonExact,
                isInvalid
                    ? StaticFieldV2ClosedConstructionIssue.ConstraintPortfolioInvalid
                    : StaticFieldV2ClosedConstructionIssue.ConstraintPortfolioNonExact,
                nameBinding,
                ancestryPortfolio,
                constraintPortfolio,
                interfaceImplementationPortfolio,
                constraintPortfolio.ReachedBound,
                constraintPortfolio.ObservedCount,
                null);
        }

        if (interfaceImplementationPortfolio is { } interfacePortfolio &&
            interfacePortfolio.ResultKind != MetadataInterfaceImplementationPortfolioResultKind.Exact)
        {
            var isInvalid =
                interfacePortfolio.ResultKind == MetadataInterfaceImplementationPortfolioResultKind.Invalid;
            return StaticFieldV2ClosedConstructionOutcome.IssueStop(
                isInvalid
                    ? StaticFieldV2ClosedConstructionResultKind.Invalid
                    : StaticFieldV2ClosedConstructionResultKind.NonExact,
                isInvalid
                    ? StaticFieldV2ClosedConstructionIssue.InterfaceImplementationPortfolioInvalid
                    : StaticFieldV2ClosedConstructionIssue.InterfaceImplementationPortfolioNonExact,
                nameBinding,
                ancestryPortfolio,
                constraintPortfolio,
                interfaceImplementationPortfolio,
                interfacePortfolio.ReachedBound,
                interfacePortfolio.ObservedCount,
                null);
        }

        var candidate = nameBinding.SelectedCandidate!;
        var ownerModule = candidate.SourceModule;
        var context = new BindContext(
            nameBinding,
            ancestryPortfolio,
            constraintPortfolio,
            interfaceImplementationPortfolio);
        if (!context.HasModule(ownerModule))
        {
            return context.Stopped(
                StaticFieldV2ClosedConstructionResultKind.Invalid,
                StaticFieldV2ClosedConstructionIssue.OwnerModuleNotInPortfolio,
                candidate.FinalTypeDefinitionToken);
        }

        var occurrences = candidate.Occurrences;
        var finalToken = candidate.FinalTypeDefinitionToken;
        foreach (var examined in occurrences)
        {
            if (examined.FinalTypeDefinitionToken != finalToken)
            {
                return context.Stopped(
                    StaticFieldV2ClosedConstructionResultKind.Invalid,
                    StaticFieldV2ClosedConstructionIssue.ArityDisagreement,
                    finalToken);
            }
        }

        var occurrence = occurrences[0];
        var expression = nameBinding.Expression;
        var expressionSegments = expression.Segments;
        var ownerSegmentCount = expression.Partitions[occurrence.PartitionIndex].FieldSegmentIndex;
        var chainSegments = occurrence.Chain.Segments;
        if (chainSegments.Length != ownerSegmentCount - occurrence.NamespaceSegmentCount)
        {
            return context.Stopped(
                StaticFieldV2ClosedConstructionResultKind.Invalid,
                StaticFieldV2ClosedConstructionIssue.ArityDisagreement,
                finalToken);
        }

        var flattenedBuilder = ImmutableArray.CreateBuilder<MetadataClosedTypeIdentity>();
        for (var index = 0; index < chainSegments.Length; index++)
        {
            var chainSegment = chainSegments[index];
            var syntaxSegment = expressionSegments[occurrence.NamespaceSegmentCount + index];
            var syntaxArguments = syntaxSegment.TypeArguments;
            if (chainSegment.IntroducedGenericArity is not { } introducedArity ||
                introducedArity != syntaxArguments.Length)
            {
                return context.Stopped(
                    StaticFieldV2ClosedConstructionResultKind.Invalid,
                    StaticFieldV2ClosedConstructionIssue.ArityDisagreement,
                    chainSegment.TypeDefinitionToken);
            }

            foreach (var syntaxArgument in syntaxArguments)
            {
                var bound = BindType(context, syntaxArgument);
                if (bound is null)
                {
                    return context.Stopped();
                }
                flattenedBuilder.Add(bound);
            }
        }

        var flattened = flattenedBuilder.ToImmutable();
        if (flattened.Length != occurrence.Chain.FinalTypeDefinition.TotalGenericArity)
        {
            return context.Stopped(
                StaticFieldV2ClosedConstructionResultKind.Invalid,
                StaticFieldV2ClosedConstructionIssue.ArityDisagreement,
                finalToken);
        }

        var classificationChain = ImmutableArray.CreateBuilder<MetadataTypeDefinitionSemanticClassificationIdentity>(
            chainSegments.Length);
        foreach (var chainSegment in chainSegments)
        {
            var classification = ancestryPortfolio.ExactClassificationOrDefault(
                ownerModule,
                chainSegment.TypeDefinitionToken);
            if (classification is not { Role: not null })
            {
                return context.Stopped(
                    StaticFieldV2ClosedConstructionResultKind.NonExact,
                    StaticFieldV2ClosedConstructionIssue.DefinitionClassificationAbsent,
                    chainSegment.TypeDefinitionToken);
            }
            classificationChain.Add(classification);
        }

        var ownerChain = classificationChain.MoveToImmutable();
        if (!TryAdmitNamed(context, ownerChain, flattened, finalToken))
        {
            return context.Stopped();
        }

        var ownerConstruction = MetadataClosedTypeIdentity.ConstructNamed(ownerChain, flattened);
        var checks = ValidateConstraints(context, ownerModule, ownerChain, flattened);
        if (checks is null)
        {
            return context.Stopped();
        }

        return StaticFieldV2ClosedConstructionOutcome.IssueExact(
            nameBinding,
            ancestryPortfolio,
            constraintPortfolio,
            interfaceImplementationPortfolio,
            ownerConstruction,
            flattened,
            checks.Value,
            context.CumulativeArgumentCount,
            finalToken);
    }

    /// <summary>Issues the exact construction of a generic base level that declared the selected static field.</summary>
    /// <remarks>
    /// Member lookup can cross a closed generic TypeSpec base and select a field physically declared on that
    /// substituted base construction. Storage selection must then target the declaring construction rather than
    /// the spelled owner, so this issuer validates the declaring construction's substituted generic constraints
    /// under the same retained binding evidence and portfolios and freezes one exact outcome around it. It never
    /// reparses, rebinds, or widens the owner name binding: the spelled owner outcome supplies every prerequisite,
    /// and a non-exact constraint disposition stops here exactly as it would for a spelled construction.
    /// </remarks>
    /// <param name="ownerConstruction">The exact spelled-owner construction outcome whose lookup selected the field.</param>
    /// <param name="declaringConstruction">The exact substituted construction of the declaring generic base level.</param>
    /// <returns>A sealed immutable outcome carrying the declaring construction, or one prefix-free typed stop.</returns>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    /// <exception cref="ArgumentException">The owner outcome is not exact or the declaring construction is not named.</exception>
    public static StaticFieldV2ClosedConstructionOutcome BindDeclaringBaseConstruction(
        StaticFieldV2ClosedConstructionOutcome ownerConstruction,
        MetadataClosedTypeIdentity declaringConstruction)
    {
        ArgumentNullException.ThrowIfNull(ownerConstruction);
        ArgumentNullException.ThrowIfNull(declaringConstruction);
        if (ownerConstruction.ResultKind != StaticFieldV2ClosedConstructionResultKind.Exact ||
            ownerConstruction.OwnerConstruction is null)
        {
            throw new ArgumentException(
                "A declaring base construction requires one exact spelled-owner construction outcome.",
                nameof(ownerConstruction));
        }
        if (declaringConstruction.FinalClassification is not { Role: not null } target)
        {
            throw new ArgumentException(
                "A declaring base construction requires one named construction with an exact final classification.",
                nameof(declaringConstruction));
        }

        var context = new BindContext(
            ownerConstruction.NameBinding,
            ownerConstruction.AncestryPortfolio,
            ownerConstruction.ConstraintPortfolio,
            ownerConstruction.InterfaceImplementationPortfolio,
            ownerConstruction.ContextualBinding);
        var declaringModule = target.SourceModule;
        if (!context.HasModule(declaringModule))
        {
            return context.Stopped(
                StaticFieldV2ClosedConstructionResultKind.Invalid,
                StaticFieldV2ClosedConstructionIssue.OwnerModuleNotInPortfolio,
                target.TypeDefinition.TypeDefinitionToken);
        }

        var definitionChain = declaringConstruction.ConstructionSegments
            .Select(static segment => segment.Classification)
            .ToImmutableArray();
        var flattenedArguments = declaringConstruction.FlattenedArguments;
        var checks = ValidateConstraints(context, declaringModule, definitionChain, flattenedArguments);
        if (checks is null)
        {
            return context.Stopped();
        }

        return StaticFieldV2ClosedConstructionOutcome.IssueExact(
            ownerConstruction.NameBinding,
            ownerConstruction.AncestryPortfolio,
            ownerConstruction.ConstraintPortfolio,
            ownerConstruction.InterfaceImplementationPortfolio,
            declaringConstruction,
            flattenedArguments,
            checks.Value,
            context.CumulativeArgumentCount,
            target.TypeDefinition.TypeDefinitionToken,
            ownerConstruction.ContextualBinding);
    }

    /// <summary>Freezes the exact closed construction of one contextually bound owner.</summary>
    /// <remarks>
    /// The contextual route binds its owner through scoped aliases, imports, and namespace levels rather than a
    /// metadata-global name, so the retained binding evidence of the issued outcome is the contextual binding
    /// itself, tagged as such in the canonical bytes. This slice constructs a definition-bound contextual owner at
    /// arity zero from the selected candidate's exact authority chain and validates its substituted constraints
    /// exactly as the explicit route does. A generic contextual owner needs the decoded whole-owner alias-target
    /// construction, which a later slice supplies; until then it is the declared typed unsupported stop rather
    /// than a guessed construction.
    /// </remarks>
    /// <param name="contextualBinding">The exact contextual binding whose selected candidate names the owner.</param>
    /// <param name="ancestryPortfolio">The ancestry authority portfolio prerequisite.</param>
    /// <param name="constraintPortfolio">The constraint-target resolution portfolio prerequisite.</param>
    /// <param name="interfaceImplementationPortfolio">The optional interface-implementation portfolio.</param>
    /// <returns>A sealed immutable outcome carrying the contextual construction, or one prefix-free typed stop.</returns>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    /// <exception cref="ArgumentException">The contextual binding is not exact with one selected candidate.</exception>
    public static StaticFieldV2ClosedConstructionOutcome BindContextualOwnerConstruction(
        StaticFieldV2ContextualBindingOutcome contextualBinding,
        MetadataAncestryAuthorityPortfolioIdentity ancestryPortfolio,
        MetadataConstraintTargetResolutionPortfolioIdentity constraintPortfolio,
        MetadataInterfaceImplementationPortfolioIdentity? interfaceImplementationPortfolio = null)
    {
        ArgumentNullException.ThrowIfNull(contextualBinding);
        ArgumentNullException.ThrowIfNull(ancestryPortfolio);
        ArgumentNullException.ThrowIfNull(constraintPortfolio);
        if (contextualBinding.ResultKind != StaticFieldV2ContextualBindingResultKind.Exact ||
            contextualBinding.SelectedCandidate is not { } candidate)
        {
            throw new ArgumentException(
                "A contextual owner construction requires one exact contextual binding with a selected candidate.",
                nameof(contextualBinding));
        }

        var context = new BindContext(
            null,
            ancestryPortfolio,
            constraintPortfolio,
            interfaceImplementationPortfolio,
            contextualBinding);
        var ownerModule = candidate.SourceModule;
        if (!context.HasModule(ownerModule))
        {
            return context.Stopped(
                StaticFieldV2ClosedConstructionResultKind.Invalid,
                StaticFieldV2ClosedConstructionIssue.OwnerModuleNotInPortfolio,
                candidate.FinalTypeDefinitionToken);
        }

        var chainSegments = candidate.Candidates[0].Chain.Segments;
        var classificationChain = ImmutableArray.CreateBuilder<MetadataTypeDefinitionSemanticClassificationIdentity>(
            chainSegments.Length);
        foreach (var chainSegment in chainSegments)
        {
            var classification = ancestryPortfolio.ExactClassificationOrDefault(
                ownerModule,
                chainSegment.TypeDefinitionToken);
            if (classification is not { Role: not null })
            {
                return context.Stopped(
                    StaticFieldV2ClosedConstructionResultKind.NonExact,
                    StaticFieldV2ClosedConstructionIssue.DefinitionClassificationAbsent,
                    chainSegment.TypeDefinitionToken);
            }
            classificationChain.Add(classification);
        }

        var ownerChain = classificationChain.MoveToImmutable();
        if (candidate.FinalTypeDefinition.TotalGenericArity != 0)
        {
            return context.Stopped(
                StaticFieldV2ClosedConstructionResultKind.Unsupported,
                StaticFieldV2ClosedConstructionIssue.ContextualConstructionRequiresDecodedAliasTarget,
                candidate.FinalTypeDefinitionToken);
        }

        var flattened = ImmutableArray<MetadataClosedTypeIdentity>.Empty;
        if (!TryAdmitNamed(context, ownerChain, flattened, candidate.FinalTypeDefinitionToken))
        {
            return context.Stopped();
        }

        var ownerConstruction = MetadataClosedTypeIdentity.ConstructNamed(ownerChain, flattened);
        var checks = ValidateConstraints(context, ownerModule, ownerChain, flattened);
        if (checks is null)
        {
            return context.Stopped();
        }

        return StaticFieldV2ClosedConstructionOutcome.IssueExact(
            null,
            ancestryPortfolio,
            constraintPortfolio,
            interfaceImplementationPortfolio,
            ownerConstruction,
            flattened,
            checks.Value,
            context.CumulativeArgumentCount,
            candidate.FinalTypeDefinitionToken,
            contextualBinding);
    }

    private static MetadataClosedTypeIdentity? BindType(BindContext context, StaticFieldV2TypeSyntax syntax) =>
        syntax.Kind switch
        {
            StaticFieldV2TypeSyntaxKind.Predefined => BindPredefined(context, syntax),
            StaticFieldV2TypeSyntaxKind.Named => BindNamed(context, syntax),
            StaticFieldV2TypeSyntaxKind.Nullable => BindNullable(context, syntax),
            StaticFieldV2TypeSyntaxKind.SzArray => BindSzArray(context, syntax),
            _ => BindMultidimensionalArray(context, syntax),
        };

    private static MetadataClosedTypeIdentity? BindPredefined(BindContext context, StaticFieldV2TypeSyntax syntax)
    {
        if (MapPredefined(syntax.PredefinedKind!.Value) is not { } primitiveKind)
        {
            context.Stop(
                StaticFieldV2ClosedConstructionResultKind.Unsupported,
                StaticFieldV2ClosedConstructionIssue.PredefinedTypeUnsupported,
                null,
                (int)syntax.PredefinedKind.Value,
                null);
            return null;
        }
        return MetadataClosedTypeIdentity.Primitive(primitiveKind);
    }

    private static MetadataClosedTypeIdentity? BindNamed(BindContext context, StaticFieldV2TypeSyntax syntax)
    {
        if (syntax.AliasQualifier is { Kind: StaticFieldV2AliasKind.Named })
        {
            context.Stop(
                StaticFieldV2ClosedConstructionResultKind.Unsupported,
                StaticFieldV2ClosedConstructionIssue.TypeArgumentAliasUnsupported,
                null,
                0,
                null);
            return null;
        }

        var nameSegments = syntax.NameSegments;
        if (!TryResolveNamedArgument(context, nameSegments, out var module, out var chain, out var namespaceCount))
        {
            return null;
        }

        var chainSegments = chain.Segments;
        var flattenedBuilder = ImmutableArray.CreateBuilder<MetadataClosedTypeIdentity>();
        for (var index = 0; index < chainSegments.Length; index++)
        {
            var chainSegment = chainSegments[index];
            var syntaxSegment = nameSegments[namespaceCount + index];
            var syntaxArguments = syntaxSegment.TypeArguments;
            if (chainSegment.IntroducedGenericArity is not { } introducedArity ||
                introducedArity != syntaxArguments.Length)
            {
                context.Stop(
                    StaticFieldV2ClosedConstructionResultKind.Invalid,
                    StaticFieldV2ClosedConstructionIssue.ArityDisagreement,
                    null,
                    0,
                    chainSegment.TypeDefinitionToken);
                return null;
            }

            foreach (var syntaxArgument in syntaxArguments)
            {
                var bound = BindType(context, syntaxArgument);
                if (bound is null)
                {
                    return null;
                }
                flattenedBuilder.Add(bound);
            }
        }

        var flattened = flattenedBuilder.ToImmutable();
        if (flattened.Length != chain.FinalTypeDefinition.TotalGenericArity)
        {
            context.Stop(
                StaticFieldV2ClosedConstructionResultKind.Invalid,
                StaticFieldV2ClosedConstructionIssue.ArityDisagreement,
                null,
                0,
                chain.FinalTypeDefinitionToken);
            return null;
        }

        var classificationChain = ImmutableArray.CreateBuilder<MetadataTypeDefinitionSemanticClassificationIdentity>(
            chainSegments.Length);
        foreach (var chainSegment in chainSegments)
        {
            var classification = context.Ancestry.ExactClassificationOrDefault(
                module,
                chainSegment.TypeDefinitionToken);
            if (classification is not { Role: not null })
            {
                context.Stop(
                    StaticFieldV2ClosedConstructionResultKind.NonExact,
                    StaticFieldV2ClosedConstructionIssue.DefinitionClassificationAbsent,
                    null,
                    0,
                    chainSegment.TypeDefinitionToken);
                return null;
            }
            classificationChain.Add(classification);
        }

        var definitionChain = classificationChain.MoveToImmutable();
        if (IsExactNullableDefinition(definitionChain, flattened) && !flattened[0].IsNonNullableValueType)
        {
            context.Stop(
                StaticFieldV2ClosedConstructionResultKind.Invalid,
                StaticFieldV2ClosedConstructionIssue.NullableElementInvalid,
                null,
                0,
                chain.FinalTypeDefinitionToken);
            return null;
        }
        if (!TryAdmitNamed(context, definitionChain, flattened, chain.FinalTypeDefinitionToken))
        {
            return null;
        }

        return MetadataClosedTypeIdentity.ConstructNamed(definitionChain, flattened);
    }

    private static MetadataClosedTypeIdentity? BindNullable(BindContext context, StaticFieldV2TypeSyntax syntax)
    {
        var element = BindType(context, syntax.ElementType!);
        if (element is null)
        {
            return null;
        }
        if (!element.IsNonNullableValueType)
        {
            context.Stop(
                StaticFieldV2ClosedConstructionResultKind.Invalid,
                StaticFieldV2ClosedConstructionIssue.NullableElementInvalid,
                null,
                0,
                null);
            return null;
        }

        if (SelectNullableDefinition(context) is not { } nullableDefinition)
        {
            context.Stop(
                StaticFieldV2ClosedConstructionResultKind.NonExact,
                StaticFieldV2ClosedConstructionIssue.NullableDefinitionAbsent,
                null,
                0,
                null);
            return null;
        }

        var definitionChain = ImmutableArray.Create(nullableDefinition);
        var flattened = ImmutableArray.Create(element);
        if (!TryAdmitNamed(context, definitionChain, flattened, nullableDefinition.TypeDefinition.TypeDefinitionToken))
        {
            return null;
        }
        return MetadataClosedTypeIdentity.ConstructNamed(definitionChain, flattened);
    }

    private static MetadataClosedTypeIdentity? BindSzArray(BindContext context, StaticFieldV2TypeSyntax syntax)
    {
        var element = BindType(context, syntax.ElementType!);
        if (element is null || !context.TryAdmitTopology(element.TopologyDepth + 1, element.TopologyNodeCount + 1))
        {
            return null;
        }
        return MetadataClosedTypeIdentity.SzArray(element);
    }

    private static MetadataClosedTypeIdentity? BindMultidimensionalArray(
        BindContext context,
        StaticFieldV2TypeSyntax syntax)
    {
        var element = BindType(context, syntax.ElementType!);
        if (element is null)
        {
            return null;
        }
        var rank = syntax.ArrayRank ?? 0;
        if (rank < 2 || rank > StaticFieldV2Limits.MaximumArrayRank)
        {
            context.Stop(
                StaticFieldV2ClosedConstructionResultKind.Invalid,
                StaticFieldV2ClosedConstructionIssue.ArrayRankInvalid,
                new EvaluationDeterministicBound(
                    ExpressionV2ContractLimits.ArrayRankBoundName,
                    StaticFieldV2Limits.MaximumArrayRank),
                rank,
                null);
            return null;
        }
        if (!context.TryAdmitTopology(element.TopologyDepth + 1, element.TopologyNodeCount + 1))
        {
            return null;
        }
        return MetadataClosedTypeIdentity.MultidimensionalArray(element, rank, [], []);
    }

    private static bool TryAdmitNamed(
        BindContext context,
        ImmutableArray<MetadataTypeDefinitionSemanticClassificationIdentity> definitionChain,
        ImmutableArray<MetadataClosedTypeIdentity> flattenedArguments,
        int relatedMetadataToken)
    {
        var depth = 1;
        var nodes = 1;
        foreach (var argument in flattenedArguments)
        {
            depth = Math.Max(depth, argument.TopologyDepth + 1);
            nodes = checked(nodes + argument.TopologyNodeCount);
        }
        if (!context.TryAdmitTopology(depth, nodes))
        {
            return false;
        }
        if (!context.TryAdmitArguments(flattenedArguments.Length))
        {
            return false;
        }
        foreach (var classification in definitionChain)
        {
            if (classification.Role == MetadataTypeDefinitionSemanticRole.ModulePseudoType)
            {
                context.Stop(
                    StaticFieldV2ClosedConstructionResultKind.NonExact,
                    StaticFieldV2ClosedConstructionIssue.DefinitionClassificationAbsent,
                    null,
                    0,
                    relatedMetadataToken);
                return false;
            }
        }
        return true;
    }

    private static bool TryResolveNamedArgument(
        BindContext context,
        ImmutableArray<StaticFieldV2TypeNameSegment> nameSegments,
        out StaticFieldMetadataModuleIdentity module,
        out MetadataNamedTypeDefinitionChainIdentity chain,
        out int namespaceSegmentCount)
    {
        module = null!;
        chain = null!;
        namespaceSegmentCount = 0;

        var accepted = false;
        var groupKeys = new HashSet<(string ModuleSha256, int TypeDefinitionToken)>();
        for (var candidateNamespaceCount = 0;
             candidateNamespaceCount < nameSegments.Length;
             candidateNamespaceCount++)
        {
            if (candidateNamespaceCount > 0 && nameSegments[candidateNamespaceCount - 1].Arity != 0)
            {
                break;
            }

            var namespaceText = JoinNamespaceText(nameSegments, candidateNamespaceCount);
            var expectedChainLength = nameSegments.Length - candidateNamespaceCount;
            foreach (var view in context.Modules)
            {
                foreach (var examined in view.Chains)
                {
                    if (!Matches(examined, expectedChainLength, namespaceText, nameSegments, candidateNamespaceCount))
                    {
                        continue;
                    }
                    if (groupKeys.Add((view.SourceModule.Sha256, examined.FinalTypeDefinitionToken)) && !accepted)
                    {
                        accepted = true;
                        module = view.SourceModule;
                        chain = examined;
                        namespaceSegmentCount = candidateNamespaceCount;
                    }
                }
            }
        }

        if (groupKeys.Count == 0)
        {
            context.Stop(
                StaticFieldV2ClosedConstructionResultKind.NonExact,
                StaticFieldV2ClosedConstructionIssue.TypeArgumentAbsent,
                null,
                0,
                null);
            return false;
        }
        if (groupKeys.Count > 1)
        {
            context.Stop(
                StaticFieldV2ClosedConstructionResultKind.Invalid,
                StaticFieldV2ClosedConstructionIssue.TypeArgumentAmbiguous,
                null,
                groupKeys.Count,
                chain.FinalTypeDefinitionToken);
            return false;
        }
        return true;
    }

    private static bool Matches(
        MetadataNamedTypeDefinitionChainIdentity chain,
        int expectedChainLength,
        string namespaceText,
        ImmutableArray<StaticFieldV2TypeNameSegment> nameSegments,
        int namespaceSegmentCount)
    {
        var chainSegments = chain.Segments;
        if (chainSegments.Length != expectedChainLength ||
            chain.IsModulePseudoType ||
            !string.Equals(chainSegments[0].RawNamespaceName, namespaceText, StringComparison.Ordinal))
        {
            return false;
        }

        for (var index = 0; index < chainSegments.Length; index++)
        {
            var chainSegment = chainSegments[index];
            var syntaxSegment = nameSegments[namespaceSegmentCount + index];
            if (chainSegment.RoslynProjection is not { } projection ||
                chainSegment.IntroducedGenericArity is not { } introducedArity ||
                !string.Equals(
                    projection.ProjectedSimpleName,
                    syntaxSegment.Identifier.DecodedText,
                    StringComparison.Ordinal) ||
                introducedArity != syntaxSegment.Arity ||
                !chainSegment.CanAppearInCSharpNamedType)
            {
                return false;
            }
        }
        return true;
    }

    private static string JoinNamespaceText(
        ImmutableArray<StaticFieldV2TypeNameSegment> nameSegments,
        int namespaceSegmentCount)
    {
        if (namespaceSegmentCount == 0)
        {
            return string.Empty;
        }
        if (namespaceSegmentCount == 1)
        {
            return nameSegments[0].Identifier.DecodedText;
        }

        var parts = new string[namespaceSegmentCount];
        for (var index = 0; index < namespaceSegmentCount; index++)
        {
            parts[index] = nameSegments[index].Identifier.DecodedText;
        }
        return string.Join('.', parts);
    }

    private static MetadataTypeDefinitionSemanticClassificationIdentity? SelectNullableDefinition(BindContext context)
    {
        if (context.Ancestry.CoreRoles is not { } coreRoles)
        {
            return null;
        }

        MetadataTypeDefinitionSemanticClassificationIdentity? selected = null;
        foreach (var entry in context.Ancestry.Entries)
        {
            if (!entry.SourceModule.Equals(coreRoles.CoreModule))
            {
                continue;
            }
            foreach (var classification in entry.Classifications)
            {
                var definition = classification.TypeDefinition;
                if (classification.Role != MetadataTypeDefinitionSemanticRole.ValueType ||
                    definition.EnclosingTypeDefinitionToken is not null ||
                    definition.TotalGenericArity != 1 ||
                    !string.Equals(definition.NamespaceName, NullableNamespaceName, StringComparison.Ordinal) ||
                    !string.Equals(definition.TypeName, NullableMetadataName, StringComparison.Ordinal))
                {
                    continue;
                }
                if (selected is not null)
                {
                    return null;
                }
                selected = classification;
            }
        }
        return selected;
    }

    private static bool IsExactNullableDefinition(
        ImmutableArray<MetadataTypeDefinitionSemanticClassificationIdentity> definitionChain,
        ImmutableArray<MetadataClosedTypeIdentity> flattenedArguments)
    {
        if (definitionChain.Length != 1 || flattenedArguments.Length != 1)
        {
            return false;
        }
        var definition = definitionChain[0].TypeDefinition;
        return definitionChain[0].Role == MetadataTypeDefinitionSemanticRole.ValueType &&
            definition.TotalGenericArity == 1 &&
            string.Equals(definition.NamespaceName, NullableNamespaceName, StringComparison.Ordinal) &&
            string.Equals(definition.TypeName, NullableMetadataName, StringComparison.Ordinal);
    }

    private static ImmutableArray<StaticFieldV2ConstraintCheckIdentity>? ValidateConstraints(
        BindContext context,
        StaticFieldMetadataModuleIdentity ownerModule,
        ImmutableArray<MetadataTypeDefinitionSemanticClassificationIdentity> definitionChain,
        ImmutableArray<MetadataClosedTypeIdentity> flattenedArguments)
    {
        var targets = context.TargetsFor(ownerModule);
        var checks = ImmutableArray.CreateBuilder<StaticFieldV2ConstraintCheckIdentity>();
        StaticFieldV2ConstraintCheckIdentity? violated = null;
        StaticFieldV2ConstraintCheckIdentity? unprovable = null;

        foreach (var classification in definitionChain)
        {
            foreach (var parameter in classification.TypeDefinition.GenericParameters)
            {
                var number = parameter.Number;
                if (number < 0 || number >= flattenedArguments.Length)
                {
                    context.Stop(
                        StaticFieldV2ClosedConstructionResultKind.Invalid,
                        StaticFieldV2ClosedConstructionIssue.ArityDisagreement,
                        null,
                        0,
                        parameter.GenericParameterToken);
                    return null;
                }

                var argument = flattenedArguments[number];
                var obligations = 0;
                if ((parameter.Flags & ReferenceTypeConstraintFlag) != 0)
                {
                    obligations++;
                    if (!Append(
                            context,
                            checks,
                            ref violated,
                            ref unprovable,
                            parameter,
                            argument,
                            argument.IsReferenceType
                                ? StaticFieldV2ConstraintDisposition.Satisfied
                                : StaticFieldV2ConstraintDisposition.Violated,
                            null,
                            argument.IsReferenceType ? ReferenceTypeSatisfiedReason : ReferenceTypeViolatedReason))
                    {
                        return null;
                    }
                }
                if ((parameter.Flags & NotNullableValueTypeConstraintFlag) != 0)
                {
                    obligations++;
                    if (!Append(
                            context,
                            checks,
                            ref violated,
                            ref unprovable,
                            parameter,
                            argument,
                            argument.IsNonNullableValueType
                                ? StaticFieldV2ConstraintDisposition.Satisfied
                                : StaticFieldV2ConstraintDisposition.Violated,
                            null,
                            argument.IsNonNullableValueType ? ValueTypeSatisfiedReason : ValueTypeViolatedReason))
                    {
                        return null;
                    }
                }
                if ((parameter.Flags & DefaultConstructorConstraintFlag) != 0)
                {
                    obligations++;
                    var proven = TryProveDefaultConstructor(context, argument, out var searchBoundReached);
                    if (searchBoundReached)
                    {
                        return null;
                    }
                    if (!Append(
                            context,
                            checks,
                            ref violated,
                            ref unprovable,
                            parameter,
                            argument,
                            proven
                                ? StaticFieldV2ConstraintDisposition.Satisfied
                                : StaticFieldV2ConstraintDisposition.Unprovable,
                            null,
                            proven ? DefaultConstructorProvenReason : DefaultConstructorUnprovenReason))
                    {
                        return null;
                    }
                }

                foreach (var target in targets)
                {
                    if (target.ConstraintRow.OwnerGenericParameterToken != parameter.GenericParameterToken)
                    {
                        continue;
                    }
                    obligations++;
                    var (disposition, reason) = EvaluateTarget(context, argument, target);
                    if (!Append(
                            context,
                            checks,
                            ref violated,
                            ref unprovable,
                            parameter,
                            argument,
                            disposition,
                            target,
                            reason))
                    {
                        return null;
                    }
                }

                if (obligations == 0 &&
                    !Append(
                        context,
                        checks,
                        ref violated,
                        ref unprovable,
                        parameter,
                        argument,
                        StaticFieldV2ConstraintDisposition.Satisfied,
                        null,
                        null))
                {
                    return null;
                }
            }
        }

        if (violated is not null)
        {
            context.Stop(
                StaticFieldV2ClosedConstructionResultKind.Invalid,
                StaticFieldV2ClosedConstructionIssue.ConstraintViolated,
                null,
                0,
                violated.Parameter.GenericParameterToken,
                violated);
            return null;
        }
        if (unprovable is not null)
        {
            context.Stop(
                StaticFieldV2ClosedConstructionResultKind.NonExact,
                StaticFieldV2ClosedConstructionIssue.ConstraintUnprovable,
                null,
                0,
                unprovable.Parameter.GenericParameterToken,
                unprovable);
            return null;
        }
        return checks.ToImmutable();
    }

    private static bool Append(
        BindContext context,
        ImmutableArray<StaticFieldV2ConstraintCheckIdentity>.Builder checks,
        ref StaticFieldV2ConstraintCheckIdentity? violated,
        ref StaticFieldV2ConstraintCheckIdentity? unprovable,
        MetadataGenericParameterTableRowIdentity parameter,
        MetadataClosedTypeIdentity argument,
        StaticFieldV2ConstraintDisposition disposition,
        MetadataConstraintTargetResolutionIdentity? target,
        string? reason)
    {
        if (checks.Count >= StaticFieldV2Limits.MaximumGenericConstraintCount)
        {
            context.Stop(
                StaticFieldV2ClosedConstructionResultKind.NonExact,
                StaticFieldV2ClosedConstructionIssue.ConstraintCheckBoundReached,
                new EvaluationDeterministicBound(
                    ExpressionV2ContractLimits.GenericConstraintCountBoundName,
                    StaticFieldV2Limits.MaximumGenericConstraintCount),
                StaticFieldV2Limits.MaximumGenericConstraintCount + 1,
                parameter.GenericParameterToken);
            return false;
        }

        var check = StaticFieldV2ClosedConstructionOutcome.IssueCheck(
            parameter,
            argument,
            disposition,
            target,
            reason);
        checks.Add(check);
        if (disposition == StaticFieldV2ConstraintDisposition.Violated)
        {
            violated ??= check;
        }
        else if (disposition == StaticFieldV2ConstraintDisposition.Unprovable)
        {
            unprovable ??= check;
        }
        return true;
    }

    private static (StaticFieldV2ConstraintDisposition Disposition, string Reason) EvaluateTarget(
        BindContext context,
        MetadataClosedTypeIdentity argument,
        MetadataConstraintTargetResolutionIdentity target)
    {
        if (target.Kind == MetadataConstraintTargetKind.TypeReferenceUnresolved)
        {
            return (StaticFieldV2ConstraintDisposition.Unprovable, UnresolvedReferenceTargetReason);
        }
        if (target.Kind == MetadataConstraintTargetKind.TypeSpecification)
        {
            return (StaticFieldV2ConstraintDisposition.Unprovable, TypeSpecificationTargetReason);
        }

        var targetModule = target.TargetModule!;
        var targetDefinition = target.TargetTypeDefinition!;
        var targetClassification = context.Ancestry.ExactClassificationOrDefault(
            targetModule,
            targetDefinition.TypeDefinitionToken);
        if (targetClassification?.Role is not { } targetRole)
        {
            return (StaticFieldV2ConstraintDisposition.Unprovable, TargetClassificationAbsentReason);
        }
        if (targetRole == MetadataTypeDefinitionSemanticRole.Interface)
        {
            return EvaluateInterfaceTarget(context, argument, targetModule, targetDefinition);
        }
        if (context.Ancestry.CoreRoles is { } coreRoles &&
            targetModule.Equals(coreRoles.CoreModule) &&
            targetDefinition.Equals(coreRoles.SystemObject))
        {
            return (StaticFieldV2ConstraintDisposition.Satisfied, BaseDefinitionExactReason);
        }
        if (argument.FinalClassification is not { } argumentClassification)
        {
            return (StaticFieldV2ConstraintDisposition.Unprovable, NonNamedArgumentReason);
        }
        if (argumentClassification.SourceModule.Equals(targetModule) &&
            argumentClassification.TypeDefinition.Equals(targetDefinition))
        {
            return (StaticFieldV2ConstraintDisposition.Satisfied, BaseDefinitionExactReason);
        }

        var ancestry = context.Ancestry.ExactAncestryChainOrDefault(
            argumentClassification.SourceModule,
            argumentClassification.TypeDefinition.TypeDefinitionToken);
        if (ancestry is null)
        {
            return (StaticFieldV2ConstraintDisposition.Unprovable, BaseDefinitionAncestryIncompleteReason);
        }
        foreach (var edge in ancestry.Edges)
        {
            if (edge.SourceModule.Equals(targetModule) && edge.Owner.Equals(targetDefinition))
            {
                return (StaticFieldV2ConstraintDisposition.Satisfied, BaseDefinitionReachedReason);
            }
        }

        // A base-class constraint target absent from the argument's base chain is a definite non-derivation
        // only when the chain terminated on a complete root (System.Object, an interface root, or the module
        // pseudo-type). Every incomplete terminal -- a generic TypeSpec base retained for later construction,
        // an unresolved TypeRef base, an invalid missing base, a cycle, or the depth bound -- is incomplete
        // evidence, so the constraint is Unprovable rather than Violated. This mirrors the assignability
        // base-chain walk (StaticFieldV2AssignabilityBinder.WalkBaseChain) and the interface-target closure
        // discipline, keeping "absence requires complete evidence" from being applied backwards.
        return ancestry.TerminalKind switch
        {
            MetadataAncestryChainTerminalKind.SystemObjectReached or
                MetadataAncestryChainTerminalKind.InterfaceRoot or
                MetadataAncestryChainTerminalKind.ModulePseudoTypeRoot =>
                (StaticFieldV2ConstraintDisposition.Violated, BaseDefinitionUnreachableReason),
            _ => (StaticFieldV2ConstraintDisposition.Unprovable, BaseDefinitionAncestryIncompleteReason),
        };
    }

    private static (StaticFieldV2ConstraintDisposition Disposition, string Reason) EvaluateInterfaceTarget(
        BindContext context,
        MetadataClosedTypeIdentity argument,
        StaticFieldMetadataModuleIdentity targetModule,
        MetadataTypeDefinitionAuthorityIdentity targetDefinition)
    {
        if (context.InterfaceImplementations is not { } portfolio)
        {
            return (StaticFieldV2ConstraintDisposition.Unprovable, InterfaceTargetReason);
        }
        if (argument.FinalClassification is not { } argumentClassification)
        {
            return (StaticFieldV2ConstraintDisposition.Unprovable, NonNamedArgumentReason);
        }

        var argumentModule = argumentClassification.SourceModule;
        var argumentToken = argumentClassification.TypeDefinition.TypeDefinitionToken;
        return portfolio.Implements(
            argumentModule,
            argumentToken,
            targetModule,
            targetDefinition.TypeDefinitionToken) switch
        {
            MetadataInterfaceImplementationAnswer.Yes =>
                (StaticFieldV2ConstraintDisposition.Satisfied, InterfaceImplementedReason),
            MetadataInterfaceImplementationAnswer.No =>
                (StaticFieldV2ConstraintDisposition.Violated, InterfaceNotImplementedReason),
            _ => (
                StaticFieldV2ConstraintDisposition.Unprovable,
                ClosureTerminalReason(portfolio.ExactImplementedInterfacesOrDefault(argumentModule, argumentToken))),
        };
    }

    private static string ClosureTerminalReason(MetadataInterfaceImplementationClosureIdentity? closure) =>
        closure?.TerminalKind switch
        {
            MetadataInterfaceImplementationClosureTerminalKind.UnresolvedReferenceReached =>
                $"{InterfaceClosureReasonPrefix}unresolved-reference-reached",
            MetadataInterfaceImplementationClosureTerminalKind.GenericInterfaceReached =>
                $"{InterfaceClosureReasonPrefix}generic-interface-reached",
            MetadataInterfaceImplementationClosureTerminalKind.CycleDetected =>
                $"{InterfaceClosureReasonPrefix}cycle-detected",
            MetadataInterfaceImplementationClosureTerminalKind.DepthBoundReached =>
                $"{InterfaceClosureReasonPrefix}depth-bound-reached",
            _ => InterfaceClosureUnavailableReason,
        };

    private static bool TryProveDefaultConstructor(
        BindContext context,
        MetadataClosedTypeIdentity argument,
        out bool searchBoundReached)
    {
        searchBoundReached = false;
        if (argument.FinalClassification is not { } classification)
        {
            return false;
        }

        var declaringToken = classification.TypeDefinition.TypeDefinitionToken;
        var examined = 0;
        foreach (var view in context.Modules)
        {
            if (!view.SourceModule.Equals(classification.SourceModule))
            {
                continue;
            }
            foreach (var method in view.DefinitionAuthority.MethodDefinitions)
            {
                if (method.DeclaringTypeDefinitionToken != declaringToken)
                {
                    continue;
                }
                examined++;
                if (examined > StaticFieldV2Limits.MaximumDefaultConstructorSearchCount)
                {
                    context.Stop(
                        StaticFieldV2ClosedConstructionResultKind.NonExact,
                        StaticFieldV2ClosedConstructionIssue.DefaultConstructorSearchBoundReached,
                        new EvaluationDeterministicBound(
                            ExpressionV2ContractLimits.DefaultConstructorSearchCountBoundName,
                            StaticFieldV2Limits.MaximumDefaultConstructorSearchCount),
                        StaticFieldV2Limits.MaximumDefaultConstructorSearchCount + 1,
                        declaringToken);
                    searchBoundReached = true;
                    return false;
                }

                var row = method.TableRow;
                if (row.IsInstance &&
                    !row.IsGeneric &&
                    row.ParameterCount == 0 &&
                    (row.Observation.Attributes & MethodMemberAccessMask) == MethodPublicAccess &&
                    string.Equals(row.Observation.Name, ConstructorName, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static MetadataPrimitiveTypeKind? MapPredefined(StaticFieldV2PredefinedTypeKind kind) => kind switch
    {
        StaticFieldV2PredefinedTypeKind.Boolean => MetadataPrimitiveTypeKind.Boolean,
        StaticFieldV2PredefinedTypeKind.Byte => MetadataPrimitiveTypeKind.UInt8,
        StaticFieldV2PredefinedTypeKind.SByte => MetadataPrimitiveTypeKind.Int8,
        StaticFieldV2PredefinedTypeKind.Int16 => MetadataPrimitiveTypeKind.Int16,
        StaticFieldV2PredefinedTypeKind.UInt16 => MetadataPrimitiveTypeKind.UInt16,
        StaticFieldV2PredefinedTypeKind.Int32 => MetadataPrimitiveTypeKind.Int32,
        StaticFieldV2PredefinedTypeKind.UInt32 => MetadataPrimitiveTypeKind.UInt32,
        StaticFieldV2PredefinedTypeKind.Int64 => MetadataPrimitiveTypeKind.Int64,
        StaticFieldV2PredefinedTypeKind.UInt64 => MetadataPrimitiveTypeKind.UInt64,
        StaticFieldV2PredefinedTypeKind.NativeInt => MetadataPrimitiveTypeKind.NativeInt,
        StaticFieldV2PredefinedTypeKind.NativeUInt => MetadataPrimitiveTypeKind.NativeUInt,
        StaticFieldV2PredefinedTypeKind.Char => MetadataPrimitiveTypeKind.Char,
        StaticFieldV2PredefinedTypeKind.Single => MetadataPrimitiveTypeKind.Single,
        StaticFieldV2PredefinedTypeKind.Double => MetadataPrimitiveTypeKind.Double,
        StaticFieldV2PredefinedTypeKind.String => MetadataPrimitiveTypeKind.String,
        StaticFieldV2PredefinedTypeKind.Object => MetadataPrimitiveTypeKind.Object,
        _ => null,
    };

    private sealed class BindContext
    {
        private readonly StaticFieldV2TypeNameBindingOutcome? nameBinding;
        private readonly StaticFieldV2ContextualBindingOutcome? contextualBinding;
        private readonly MetadataConstraintTargetResolutionPortfolioIdentity constraintPortfolio;
        private readonly Dictionary<string, ImmutableArray<MetadataConstraintTargetResolutionIdentity>> targetsByModule;
        private StaticFieldV2ClosedConstructionResultKind stopResultKind;
        private StaticFieldV2ClosedConstructionIssue stopIssue;
        private EvaluationDeterministicBound? stopBound;
        private int stopObservedCount;
        private int? stopRelatedMetadataToken;
        private StaticFieldV2ConstraintCheckIdentity? stopCheck;

        internal BindContext(
            StaticFieldV2TypeNameBindingOutcome? nameBinding,
            MetadataAncestryAuthorityPortfolioIdentity ancestryPortfolio,
            MetadataConstraintTargetResolutionPortfolioIdentity constraintPortfolio,
            MetadataInterfaceImplementationPortfolioIdentity? interfaceImplementationPortfolio,
            StaticFieldV2ContextualBindingOutcome? contextualBinding = null)
        {
            this.nameBinding = nameBinding;
            this.contextualBinding = contextualBinding;
            Ancestry = ancestryPortfolio;
            this.constraintPortfolio = constraintPortfolio;
            InterfaceImplementations = interfaceImplementationPortfolio;

            var chainEntries = ancestryPortfolio.ResolutionPortfolio.ChainPortfolio.Entries;
            var views = ImmutableArray.CreateBuilder<ChainModuleView>(chainEntries.Length);
            foreach (var entry in chainEntries)
            {
                views.Add(new ChainModuleView(
                    entry.SourceModule,
                    entry.ChainCatalog.Chains,
                    entry.ChainCatalog.DefinitionAuthority));
            }
            Modules = views.MoveToImmutable();

            targetsByModule = [];
            foreach (var entry in constraintPortfolio.Entries)
            {
                targetsByModule[entry.SourceModule.Sha256] = entry.Targets;
            }
        }

        internal MetadataAncestryAuthorityPortfolioIdentity Ancestry { get; }

        internal MetadataInterfaceImplementationPortfolioIdentity? InterfaceImplementations { get; }

        internal ImmutableArray<ChainModuleView> Modules { get; }

        internal int CumulativeArgumentCount { get; private set; }

        internal bool HasModule(StaticFieldMetadataModuleIdentity module)
        {
            if (!targetsByModule.ContainsKey(module.Sha256))
            {
                return false;
            }
            foreach (var entry in Ancestry.Entries)
            {
                if (entry.SourceModule.Equals(module))
                {
                    return true;
                }
            }
            return false;
        }

        internal ImmutableArray<MetadataConstraintTargetResolutionIdentity> TargetsFor(
            StaticFieldMetadataModuleIdentity module) =>
            targetsByModule.TryGetValue(module.Sha256, out var targets) ? targets : [];

        internal bool TryAdmitTopology(int depth, int nodes)
        {
            if (depth > StaticFieldV2Limits.MaximumClosedTypeTopologyDepth)
            {
                Stop(
                    StaticFieldV2ClosedConstructionResultKind.NonExact,
                    StaticFieldV2ClosedConstructionIssue.TopologyDepthBoundReached,
                    new EvaluationDeterministicBound(
                        ExpressionV2ContractLimits.ClosedTypeTopologyDepthBoundName,
                        StaticFieldV2Limits.MaximumClosedTypeTopologyDepth),
                    StaticFieldV2Limits.MaximumClosedTypeTopologyDepth + 1,
                    null);
                return false;
            }
            if (nodes > StaticFieldV2Limits.MaximumClosedTypeTopologyNodeCount)
            {
                Stop(
                    StaticFieldV2ClosedConstructionResultKind.NonExact,
                    StaticFieldV2ClosedConstructionIssue.TopologyNodeCountBoundReached,
                    new EvaluationDeterministicBound(
                        ExpressionV2ContractLimits.ClosedTypeTopologyNodeCountBoundName,
                        StaticFieldV2Limits.MaximumClosedTypeTopologyNodeCount),
                    StaticFieldV2Limits.MaximumClosedTypeTopologyNodeCount + 1,
                    null);
                return false;
            }
            return true;
        }

        internal bool TryAdmitArguments(int additionalCount)
        {
            var total = checked(CumulativeArgumentCount + additionalCount);
            if (total > StaticFieldV2Limits.MaximumTypeSpecificationArgumentCount)
            {
                Stop(
                    StaticFieldV2ClosedConstructionResultKind.NonExact,
                    StaticFieldV2ClosedConstructionIssue.ArgumentCountBoundReached,
                    new EvaluationDeterministicBound(
                        ExpressionV2ContractLimits.TypeSpecificationArgumentCountBoundName,
                        StaticFieldV2Limits.MaximumTypeSpecificationArgumentCount),
                    StaticFieldV2Limits.MaximumTypeSpecificationArgumentCount + 1,
                    null);
                return false;
            }
            CumulativeArgumentCount = total;
            return true;
        }

        internal void Stop(
            StaticFieldV2ClosedConstructionResultKind resultKind,
            StaticFieldV2ClosedConstructionIssue issue,
            EvaluationDeterministicBound? reachedBound,
            int observedCount,
            int? relatedMetadataToken,
            StaticFieldV2ConstraintCheckIdentity? decisiveCheck = null)
        {
            if (stopIssue != StaticFieldV2ClosedConstructionIssue.None)
            {
                return;
            }
            stopResultKind = resultKind;
            stopIssue = issue;
            stopBound = reachedBound;
            stopObservedCount = observedCount;
            stopRelatedMetadataToken = relatedMetadataToken;
            stopCheck = decisiveCheck;
        }

        internal StaticFieldV2ClosedConstructionOutcome Stopped(
            StaticFieldV2ClosedConstructionResultKind resultKind,
            StaticFieldV2ClosedConstructionIssue issue,
            int? relatedMetadataToken)
        {
            Stop(resultKind, issue, null, 0, relatedMetadataToken);
            return Stopped();
        }

        internal StaticFieldV2ClosedConstructionOutcome Stopped()
        {
            if (stopIssue == StaticFieldV2ClosedConstructionIssue.None)
            {
                throw new InvalidOperationException("A closed-construction stop requires one recorded typed issue.");
            }
            return StaticFieldV2ClosedConstructionOutcome.IssueStop(
                stopResultKind,
                stopIssue,
                nameBinding,
                Ancestry,
                constraintPortfolio,
                InterfaceImplementations,
                stopBound,
                stopObservedCount,
                stopRelatedMetadataToken,
                stopCheck,
                contextualBinding);
        }
    }

    private sealed record ChainModuleView(
        StaticFieldMetadataModuleIdentity SourceModule,
        ImmutableArray<MetadataNamedTypeDefinitionChainIdentity> Chains,
        MetadataDefinitionAuthorityCatalogIdentity DefinitionAuthority);
}
