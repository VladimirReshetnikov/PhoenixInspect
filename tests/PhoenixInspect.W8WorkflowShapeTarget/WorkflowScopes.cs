using System.Runtime.CompilerServices;
using AmbiguousStageSlot = PhoenixInspect.W8WorkflowShapeTarget.FirstAmbiguousStage;
using GroundStageAlias = PhoenixInspect.W8WorkflowShapeTarget.StageMarkers<PhoenixInspect.W8WorkflowShapeTarget.StepContext>;

namespace PhoenixInspect.W8WorkflowShapeTarget;

/// <summary>Pauses where two competing aliases survive for one short workflow spelling.</summary>
/// <remarks>This is a frame probe and not a frame-value product contract.</remarks>
public static class AmbiguousAliasProbe
{
    /// <summary>Reads one competing alias and pauses for the full dump.</summary>
    /// <param name="profile">The selected truth-gate profile.</param>
    /// <returns>A process exit code if the pause unexpectedly returns.</returns>
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    public static int Run(string profile) => WorkflowPause.WaitForDump(profile, AmbiguousStageSlot.Sentinel);
}

/// <summary>Pauses inside a generic method whose ground alias names a metadata-only literal.</summary>
/// <remarks>This is a frame probe and not a frame-value product contract.</remarks>
public static class GroundLiteralProbe
{
    /// <summary>Enters a generic method, reads the ground literal, and pauses for the full dump.</summary>
    /// <typeparam name="TMethod">The method type argument the generic frame carries.</typeparam>
    /// <param name="profile">The selected truth-gate profile.</param>
    /// <returns>A process exit code if the pause unexpectedly returns.</returns>
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    public static int Run<TMethod>(string profile)
        where TMethod : class
    {
        var marker = GroundStageAlias.CompletedMarker;
        var methodWitness = typeof(TMethod);
        GC.KeepAlive(methodWitness);
        return WorkflowPause.WaitForDump(profile, (int)marker);
    }
}

/// <summary>Pauses with one exact memory-homed local as the selected frame-value root.</summary>
/// <remarks>This is a frame probe and not a frame-value product contract.</remarks>
public static class FrameValueProbe
{
    /// <summary>Homes one named local in frame memory and pauses for the full dump.</summary>
    /// <param name="profile">The selected truth-gate profile.</param>
    /// <param name="seed">The value the homed local retains.</param>
    /// <returns>A process exit code if the pause unexpectedly returns.</returns>
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    public static unsafe int Run(string profile, int seed)
    {
        var stageLocal = seed;
        var alternateLocal = seed ^ 0x5A_5A_5A_5A;
        var homed = &stageLocal;
        var alternateHomed = &alternateLocal;
        var retained = *homed ^ *alternateHomed;
        return WorkflowPause.WaitForDump(profile, retained);
    }
}
