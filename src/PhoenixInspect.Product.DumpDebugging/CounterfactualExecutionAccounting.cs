using System.Collections.Immutable;
using PhoenixInspect.Core.Abstractions;
using PhoenixInspect.Core.Execution;

namespace PhoenixInspect.Product.DumpDebugging;

/// <summary>Classifies whether one deterministic execution bound applied on the represented path.</summary>
/// <remarks>This W4 schema is closed and requires versioning before adding or reinterpreting a member.</remarks>
public enum CounterfactualBoundStatus
{
    /// <summary>The bound does not apply to this result origin or value.</summary>
    NotApplicable = 1,

    /// <summary>The limit was configured, but execution stopped before the guarded operation was reached.</summary>
    NotReached = 2,

    /// <summary>The guarded operation ran within the configured limit.</summary>
    Applied = 3,

    /// <summary>The guarded operation attempted to continue beyond the configured limit.</summary>
    Exhausted = 4,
}

/// <summary>
/// Projects path-accurate deterministic accounting without exposing a budget policy or operational machine state.
/// </summary>
/// <remarks>
/// Nullable numeric facts are intentional. Inapplicable bounds carry no numbers; a not-reached bound may retain its
/// configured limit but never fabricates used or remaining work. Traversal charges identify only actually consumed
/// discovery units, and an exhausted traversal additionally identifies its first rejected unit.
/// This is an unstable W4 projection rather than a committed public serialization contract.
/// </remarks>
public sealed class CounterfactualExecutionAccounting
{
    private readonly ImmutableArray<MethodGraphTraversalCharge> traversalCharges;

    private CounterfactualExecutionAccounting(
        CounterfactualBoundStatus instructionStatus,
        long? instructionLimit,
        long? instructionUsed,
        long? instructionRemaining,
        CounterfactualBoundStatus traversalStatus,
        int? traversalLimit,
        int? traversalUsed,
        int? traversalRemaining,
        ImmutableArray<MethodGraphTraversalCharge> traversalCharges,
        MethodGraphTraversalCharge? rejectedTraversalCharge,
        CounterfactualBoundStatus depthStatus,
        int? logicalDepthLimit,
        int? requiredLogicalDepth,
        int? observedLogicalDepthHighWater,
        int? activeFrameDepthHighWater,
        CounterfactualBoundStatus lineageStatus,
        long? lineageNodeCeiling,
        int? lineageNodeCount)
    {
        ValidateSimpleBound(
            instructionStatus,
            instructionLimit,
            instructionUsed,
            instructionRemaining,
            nameof(instructionStatus));
        var copiedCharges = CounterfactualCanonical.Copy(traversalCharges);
        ValidateTraversal(
            traversalStatus,
            traversalLimit,
            traversalUsed,
            traversalRemaining,
            copiedCharges,
            rejectedTraversalCharge);
        ValidateDepth(
            depthStatus,
            logicalDepthLimit,
            requiredLogicalDepth,
            observedLogicalDepthHighWater,
            activeFrameDepthHighWater);
        ValidateLineage(lineageStatus, lineageNodeCeiling, lineageNodeCount);

        InstructionStatus = instructionStatus;
        InstructionLimit = instructionLimit;
        InstructionUsed = instructionUsed;
        InstructionRemaining = instructionRemaining;
        TraversalStatus = traversalStatus;
        TraversalLimit = traversalLimit;
        TraversalUsed = traversalUsed;
        TraversalRemaining = traversalRemaining;
        this.traversalCharges = copiedCharges;
        RejectedTraversalCharge = rejectedTraversalCharge;
        DepthStatus = depthStatus;
        LogicalDepthLimit = logicalDepthLimit;
        RequiredLogicalDepth = requiredLogicalDepth;
        ObservedLogicalDepthHighWater = observedLogicalDepthHighWater;
        ActiveFrameDepthHighWater = activeFrameDepthHighWater;
        LineageStatus = lineageStatus;
        LineageNodeCeiling = lineageNodeCeiling;
        LineageNodeCount = lineageNodeCount;
        AllocationStatus = CounterfactualBoundStatus.NotApplicable;
    }

    /// <summary>Gets whether the instruction bound was inapplicable, unreached, applied, or exhausted.</summary>
    public CounterfactualBoundStatus InstructionStatus { get; }

    /// <summary>Gets the configured instruction-unit limit when applicable.</summary>
    public long? InstructionLimit { get; }

    /// <summary>Gets the instruction units actually consumed when the bound was reached.</summary>
    public long? InstructionUsed { get; }

    /// <summary>Gets the unused instruction units when the bound was reached.</summary>
    public long? InstructionRemaining { get; }

    /// <summary>Gets whether frozen-graph traversal was inapplicable, unreached, applied, or exhausted.</summary>
    public CounterfactualBoundStatus TraversalStatus { get; }

    /// <summary>Gets the configured graph-traversal-unit limit when applicable.</summary>
    public int? TraversalLimit { get; }

    /// <summary>Gets graph-traversal units actually consumed when traversal was reached.</summary>
    public int? TraversalUsed { get; }

    /// <summary>Gets graph-traversal units left after traversal when it was reached.</summary>
    public int? TraversalRemaining { get; }

    /// <summary>Gets a defensive copy of consumed graph-traversal charges in discovery order.</summary>
    public ImmutableArray<MethodGraphTraversalCharge> TraversalCharges =>
        CounterfactualCanonical.Copy(traversalCharges);

    /// <summary>Gets the first unconsumed traversal unit after exhaustion, or <see langword="null"/>.</summary>
    public MethodGraphTraversalCharge? RejectedTraversalCharge { get; }

    /// <summary>Gets whether logical/frame-depth accounting was inapplicable, unreached, applied, or exhausted.</summary>
    public CounterfactualBoundStatus DepthStatus { get; }

    /// <summary>Gets the configured rooted logical-depth limit when applicable.</summary>
    public int? LogicalDepthLimit { get; }

    /// <summary>Gets the frozen graph's required logical depth when structural preparation reached that fact.</summary>
    public int? RequiredLogicalDepth { get; }

    /// <summary>Gets the greatest rooted logical boundary entered during execution when observed.</summary>
    public int? ObservedLogicalDepthHighWater { get; }

    /// <summary>Gets the greatest number of simultaneously active interpreted frames when observed.</summary>
    public int? ActiveFrameDepthHighWater { get; }

    /// <summary>Gets whether the provenance-lineage ceiling was inapplicable, unreached, applied, or exhausted.</summary>
    public CounterfactualBoundStatus LineageStatus { get; }

    /// <summary>Gets the configured maximum reachable lineage-node count when applicable.</summary>
    public long? LineageNodeCeiling { get; }

    /// <summary>Gets the reachable lineage-node count when lineage materialization was reached.</summary>
    public int? LineageNodeCount { get; }

    /// <summary>
    /// Gets allocation accounting, always <see cref="CounterfactualBoundStatus.NotApplicable"/> for this read-only
    /// W4 profile.
    /// </summary>
    public CounterfactualBoundStatus AllocationStatus { get; }

    private static CounterfactualExecutionAccounting Create(
        CounterfactualBoundStatus instructionStatus,
        long? instructionLimit,
        long? instructionUsed,
        long? instructionRemaining,
        CounterfactualBoundStatus traversalStatus,
        int? traversalLimit,
        int? traversalUsed,
        int? traversalRemaining,
        ImmutableArray<MethodGraphTraversalCharge> traversalCharges,
        MethodGraphTraversalCharge? rejectedTraversalCharge,
        CounterfactualBoundStatus depthStatus,
        int? logicalDepthLimit,
        int? requiredLogicalDepth,
        int? observedLogicalDepthHighWater,
        int? activeFrameDepthHighWater,
        CounterfactualBoundStatus lineageStatus,
        long? lineageNodeCeiling,
        int? lineageNodeCount) =>
        new(
            instructionStatus,
            instructionLimit,
            instructionUsed,
            instructionRemaining,
            traversalStatus,
            traversalLimit,
            traversalUsed,
            traversalRemaining,
            traversalCharges,
            rejectedTraversalCharge,
            depthStatus,
            logicalDepthLimit,
            requiredLogicalDepth,
            observedLogicalDepthHighWater,
            activeFrameDepthHighWater,
            lineageStatus,
            lineageNodeCeiling,
            lineageNodeCount);

    internal static CounterfactualExecutionAccounting CreateStandaloneTargetOutcome(
        CounterfactualTargetOutcomeFragment fragment)
    {
        ArgumentNullException.ThrowIfNull(fragment);
        if (!CounterfactualExecutionValue.IsCanonicalTargetOutcome(fragment))
        {
            throw new ArgumentException("Standalone accounting requires one canonical W4.7 fragment.", nameof(fragment));
        }

        return
        Create(
            CounterfactualBoundStatus.Applied,
            fragment.InitialInstructionUnits,
            fragment.UsedInstructionUnits,
            fragment.RemainingInstructionUnits,
            CounterfactualBoundStatus.NotApplicable,
            null,
            null,
            null,
            [],
            null,
            CounterfactualBoundStatus.NotApplicable,
            null,
            null,
            null,
            null,
            CounterfactualBoundStatus.NotApplicable,
            null,
            null);
    }

    internal static CounterfactualExecutionAccounting CreateFacadeRejection() =>
        Create(
            CounterfactualBoundStatus.NotApplicable,
            null,
            null,
            null,
            CounterfactualBoundStatus.NotApplicable,
            null,
            null,
            null,
            [],
            null,
            CounterfactualBoundStatus.NotApplicable,
            null,
            null,
            null,
            null,
            CounterfactualBoundStatus.NotApplicable,
            null,
            null);

    internal static CounterfactualExecutionAccounting CreateRooted<TMemory>(
        CounterfactualMethodPlan<TMemory> plan,
        CounterfactualBoundStatus instructionStatus,
        long? instructionUsed,
        long? instructionRemaining,
        int? observedLogicalDepthHighWater,
        int? activeFrameDepthHighWater,
        CounterfactualBoundStatus lineageStatus,
        int? lineageNodeCount)
        where TMemory : IPersistentMemoryState<TMemory>
    {
        ArgumentNullException.ThrowIfNull(plan);
        var request = plan.Request;
        if (instructionStatus == CounterfactualBoundStatus.NotApplicable ||
            lineageStatus == CounterfactualBoundStatus.NotApplicable ||
            plan.TraversalLimit != request.TraversalLimit ||
            plan.TraversalUsed + plan.TraversalRemaining != plan.TraversalLimit ||
            plan.RequiredLogicalDepth > request.LogicalDepthLimit)
        {
            throw new ArgumentException("The issued plan accounting does not match its canonical request.", nameof(plan));
        }

        var depthStatus = observedLogicalDepthHighWater.HasValue || activeFrameDepthHighWater.HasValue
            ? CounterfactualBoundStatus.Applied
            : CounterfactualBoundStatus.NotReached;
        return Create(
            instructionStatus,
            request.InstructionLimit,
            instructionUsed,
            instructionRemaining,
            CounterfactualBoundStatus.Applied,
            plan.TraversalLimit,
            plan.TraversalUsed,
            plan.TraversalRemaining,
            plan.TraversalCharges,
            null,
            depthStatus,
            request.LogicalDepthLimit,
            plan.RequiredLogicalDepth,
            observedLogicalDepthHighWater,
            activeFrameDepthHighWater,
            lineageStatus,
            request.LineageNodeCeiling,
            lineageNodeCount);
    }

    private static void ValidateSimpleBound(
        CounterfactualBoundStatus status,
        long? limit,
        long? used,
        long? remaining,
        string parameterName)
    {
        ValidateStatus(status, parameterName);
        if (status == CounterfactualBoundStatus.NotApplicable)
        {
            Require(limit is null && used is null && remaining is null, "An inapplicable bound has no numeric facts.");
            return;
        }

        Require(limit is >= 0, "An applicable bound requires a nonnegative limit.");
        if (status == CounterfactualBoundStatus.NotReached)
        {
            Require(used is null && remaining is null, "A not-reached bound cannot claim used or remaining work.");
            return;
        }

        Require(used is >= 0 && remaining is >= 0 && used <= limit && remaining == limit - used,
            "Reached bound accounting must conserve its configured limit.");
        if (status == CounterfactualBoundStatus.Exhausted)
        {
            Require(used == limit && remaining == 0, "An exhausted bound must consume its complete limit.");
        }
    }

    private static void ValidateTraversal(
        CounterfactualBoundStatus status,
        int? limit,
        int? used,
        int? remaining,
        ImmutableArray<MethodGraphTraversalCharge> charges,
        MethodGraphTraversalCharge? rejected)
    {
        ValidateSimpleBound(status, limit, used, remaining, nameof(status));
        if (status is CounterfactualBoundStatus.NotApplicable or CounterfactualBoundStatus.NotReached)
        {
            Require(charges.IsEmpty && rejected is null, "Unreached traversal has no consumed or rejected units.");
            return;
        }

        Require(used == charges.Length, "Traversal used units must equal the retained charge count.");
        Require(charges.Where((charge, index) => charge is null || charge.Ordinal != index).Any() is false,
            "Traversal charges must be non-null with contiguous ordinals.");
        Require((status == CounterfactualBoundStatus.Exhausted) == (rejected is not null),
            "Exactly an exhausted traversal requires one rejected charge.");
        Require(rejected is null || rejected.Ordinal == charges.Length,
            "The rejected traversal charge must immediately follow consumed charges.");
    }

    private static void ValidateDepth(
        CounterfactualBoundStatus status,
        int? limit,
        int? required,
        int? logicalHighWater,
        int? activeFrameHighWater)
    {
        ValidateStatus(status, nameof(status));
        if (status == CounterfactualBoundStatus.NotApplicable)
        {
            Require(limit is null && required is null && logicalHighWater is null && activeFrameHighWater is null,
                "Inapplicable depth has no numeric facts.");
            return;
        }

        Require(limit is >= 0, "Applicable rooted depth requires a nonnegative limit.");
        if (status == CounterfactualBoundStatus.NotReached)
        {
            Require(required is >= 1 && required <= limit && logicalHighWater is null && activeFrameHighWater is null,
                "Not-reached rooted depth retains prepared graph depth but cannot claim execution high-water facts.");
            return;
        }

        if (status == CounterfactualBoundStatus.Applied)
        {
            Require(required is >= 1 && logicalHighWater is >= 1 && activeFrameHighWater is >= 1,
                "Applied depth requires positive graph and high-water facts.");
            Require(activeFrameHighWater <= logicalHighWater,
                "Active interpreted frames cannot exceed rooted logical depth.");
            Require(required <= limit && logicalHighWater <= required && activeFrameHighWater <= required,
                "Applied high-water facts must fit the prepared graph's required depth.");
        }
        else
        {
            Require(required > limit && logicalHighWater is null && activeFrameHighWater is null,
                "Preactivation depth exhaustion requires graph depth above the limit and no invented high-water facts.");
        }
    }

    private static void ValidateLineage(CounterfactualBoundStatus status, long? ceiling, int? count)
    {
        ValidateStatus(status, nameof(status));
        if (status == CounterfactualBoundStatus.NotApplicable)
        {
            Require(ceiling is null && count is null, "Inapplicable lineage has no numeric facts.");
            return;
        }

        Require(ceiling is >= 0, "Applicable lineage requires a nonnegative ceiling.");
        if (status == CounterfactualBoundStatus.NotReached)
        {
            Require(count is null, "Not-reached lineage cannot claim a node count.");
            return;
        }

        Require(count is >= 0, "Reached lineage requires a nonnegative node count.");
        if (status == CounterfactualBoundStatus.Applied)
        {
            Require(count <= ceiling, "Applied lineage must fit its retained ceiling.");
        }
        else
        {
            Require(count > ceiling, "Exhausted lineage must truthfully retain the violating node count.");
        }
    }

    private static void ValidateStatus(CounterfactualBoundStatus status, string parameterName)
    {
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new ArgumentException(message);
        }
    }
}
