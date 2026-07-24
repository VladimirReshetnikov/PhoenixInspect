using System.Runtime.CompilerServices;
using static PhoenixInspect.W8CoordinatorShapeTarget.CoordinatorTotals;
using RegionsNs = PhoenixInspect.W8CoordinatorShapeTarget.Values;

namespace PhoenixInspect.W8CoordinatorShapeTarget;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (Environment.GetEnvironmentVariable("OPENAI_API_KEY") is not null ||
            Environment.GetEnvironmentVariable("GITHUB_TOKEN") is not null ||
            Environment.GetEnvironmentVariable("GH_TOKEN") is not null ||
            Environment.GetEnvironmentVariable("INTERPRETER_TEST_ARTIFACT_CANARY") is not null)
        {
            Console.WriteLine("UNEXPECTED_ENVIRONMENT");
            Console.Out.Flush();
            return 90;
        }

        if (args is not ["--truth-gate", var profile])
        {
            Console.WriteLine("INVALID_ARGUMENTS");
            Console.Out.Flush();
            return 91;
        }

        return CoordinatorShapeGate.Run(profile);
    }
}

/// <summary>Holds the sole pause the coordinator-shape target uses to signal dump readiness.</summary>
/// <remarks>This is a draft target entry point and not a product API.</remarks>
public static class CoordinatorPause
{
    /// <summary>Signals readiness and pauses so an external writer can capture one full dump.</summary>
    /// <param name="profile">The selected truth-gate profile.</param>
    /// <param name="retained">One retained fixture value the pause keeps alive.</param>
    /// <returns>A process exit code if the pause unexpectedly returns.</returns>
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    public static int WaitForDump(string profile, int retained)
    {
        Console.WriteLine("READY");
        Console.Out.Flush();
        Thread.Sleep(Timeout.Infinite);
        GC.KeepAlive(profile);
        GC.KeepAlive(retained);
        return 0;
    }
}

/// <summary>Materializes the coordinator-shape object graph and enters the selected truth-gate profile.</summary>
/// <remarks>This is a draft target entry point and not a product API.</remarks>
public static class CoordinatorShapeGate
{
    /// <summary>Initializes the coordinator-shape physical fixture and enters the selected profile.</summary>
    /// <param name="profile">The predeclared truth-gate profile of exactly one corpus incident.</param>
    /// <returns>A process exit code if the target does not remain paused.</returns>
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    public static int Run(string profile)
    {
        Registry<NorthRegion>.Sentinel = 0x71_00_03_01;
        Registry<SouthRegion>.Sentinel = 0x71_00_03_02;
        Registry<EastRegion>.Sentinel = 0x71_00_03_03;
        Registry<WestRegion>.Sentinel = 0x71_00_03_04;
        Registry<RegionsNs.RegionCode>.Sentinel = 0x71_00_03_07;

        RegistryBase<WestRegion>.BaseSentinel = 0x71_00_03_0B;
        NullableSlot<int?>.Current = 0x71_00_03_0F;
        NullableSlot<RegionsNs.RegionCode?>.Current = new RegionsNs.RegionCode(0x71_00_03_10);
        DeclaredRegionTotal = 0x71_00_03_1F;

        var derived = new DerivedRegistry();
        GC.KeepAlive(derived);

        return profile switch
        {
            "coordinator-four-constructions" => CoordinatorPause.WaitForDump(profile, Registry<NorthRegion>.Sentinel),
            "coordinator-alias-argument" => AliasArgumentProbe.Run(profile),
            "coordinator-derived-base-field" => CoordinatorPause.WaitForDump(profile, DerivedRegistry.BaseSentinel),
            "coordinator-nullable-argument" => CoordinatorPause.WaitForDump(
                profile,
                NullableSlot<int?>.Current ?? 0),
            "coordinator-pdb-conflict" => AliasArgumentProbe.Run(profile),
            "coordinator-arity-disagreement" => CoordinatorPause.WaitForDump(profile, Registry<NorthRegion>.Sentinel),
            "coordinator-unavailable-slot" => CoordinatorPause.WaitForDump(profile, Registry<EastRegion>.Sentinel),
            "coordinator-property-name" => PropertyNameProbe.Run(profile),
            _ => 92,
        };
    }
}

/// <summary>Pauses where one type argument is spelled through a namespace alias.</summary>
/// <remarks>This is a draft frame probe and not a frame-value product contract.</remarks>
public static class AliasArgumentProbe
{
    /// <summary>Reads the aliased-argument construction and pauses for the full dump.</summary>
    /// <param name="profile">The selected truth-gate profile.</param>
    /// <returns>A process exit code if the pause unexpectedly returns.</returns>
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    public static int Run(string profile) =>
        CoordinatorPause.WaitForDump(profile, Registry<RegionsNs.RegionCode>.Sentinel);
}

/// <summary>Pauses where a property owns the bare spelling a declared static field does not.</summary>
/// <remarks>This is a draft frame probe and not a frame-value product contract.</remarks>
public static class PropertyNameProbe
{
    /// <summary>Reads the bare property spelling and pauses for the full dump.</summary>
    /// <param name="profile">The selected truth-gate profile.</param>
    /// <returns>A process exit code if the pause unexpectedly returns.</returns>
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    public static int Run(string profile) => CoordinatorPause.WaitForDump(profile, RegionTotal);
}
