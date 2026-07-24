using System.Runtime.CompilerServices;

// Two same-level namespace imports: the declaring assembly and the assembly that forwards the same TypeDef.
using PhoenixInspect.W8AliasTarget;
using PhoenixInspect.W8ForwarderTarget;
using static PhoenixInspect.W8BatchShapeTarget.BatchImports;

namespace PhoenixInspect.W8BatchShapeTarget;

/// <summary>Pauses where two same-level import paths converge on one physical TypeDef.</summary>
/// <remarks>This is a draft frame probe and not a frame-value product contract.</remarks>
public static class ConvergenceProbe
{
    /// <summary>Reads the converged construction's sentinel and pauses for the full dump.</summary>
    /// <param name="profile">The selected truth-gate profile.</param>
    /// <returns>A process exit code if the pause unexpectedly returns.</returns>
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    public static int Run(string profile)
    {
        var forwarderWitness = NonFriendAccessibilityOwner.PublicSentinel;
        var converged = ExternalSlot<ForwardedRequestContext>.Sentinel;
        return BatchPause.WaitForDump(profile, converged ^ forwarderWitness);
    }
}

/// <summary>Pauses where the bare nested reference head is imported by <c>using static</c>.</summary>
/// <remarks>This is a draft frame probe and not a frame-value product contract.</remarks>
public static class NestedHeadProbe
{
    /// <summary>Reads the imported nested head's label and pauses for the full dump.</summary>
    /// <param name="profile">The selected truth-gate profile.</param>
    /// <returns>A process exit code if the pause unexpectedly returns.</returns>
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    public static int Run(string profile)
    {
        var label = ImportedNestedCurrent!.Label;
        return BatchPause.WaitForDump(profile, label.Length);
    }
}

/// <summary>Pauses where the bare pending count's blocker catalog is deliberately incomplete.</summary>
/// <remarks>This is a draft frame probe and not a frame-value product contract.</remarks>
public static class PendingCountProbe
{
    /// <summary>Reads the bare pending count and pauses for the full dump.</summary>
    /// <param name="profile">The selected truth-gate profile.</param>
    /// <returns>A process exit code if the pause unexpectedly returns.</returns>
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    public static int Run(string profile) => BatchPause.WaitForDump(profile, PendingCount);
}
