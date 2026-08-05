using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

namespace PhoenixInspect.EncTestTarget;

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

        if (args is not ["--truth-gate", var profile, "--payload", var payloadDirectory])
        {
            Console.WriteLine("INVALID_ARGUMENTS");
            Console.Out.Flush();
            return 91;
        }

        return EncGate.Run(profile, payloadDirectory);
    }
}

/// <summary>Holds the sole pause the edited-process target uses to signal dump readiness.</summary>
/// <remarks>This is a target entry point and not a product API.</remarks>
public static class EncPause
{
    /// <summary>Signals readiness and pauses so an external writer can capture one full dump.</summary>
    /// <param name="profile">The selected truth-gate profile.</param>
    /// <param name="retained">One retained fixture value the pause keeps alive.</param>
    /// <returns>A process exit code if the pause unexpectedly returns.</returns>
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    public static int WaitForDump(string profile, int retained)
    {
        // The transient payload arrays this process handed to the runtime are collected before the pause, so a
        // delta copy that survives into the dump is the runtime's own retained one rather than a fixture leftover.
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);
        Console.WriteLine("READY");
        Console.Out.Flush();
        Thread.Sleep(Timeout.Infinite);
        GC.KeepAlive(profile);
        GC.KeepAlive(retained);
        return 0;
    }
}

/// <summary>Applies real compiled metadata deltas to the payload baseline assembly and enters the selected profile.</summary>
/// <remarks>
/// This is a target entry point and not a product API. The baseline assembly and every generation's delta blobs are
/// produced by the test infrastructure with the pinned compiler and handed over as payload files; this process only
/// loads, applies, verifies, and pauses. Every verification failure is its own typed exit code so the harness never
/// mistakes an unedited or wrongly edited process for a ready one: READY is printed only after the runtime observably
/// executes the edited body.
/// </remarks>
public static class EncGate
{
    /// <summary>The value the baseline sentinel method returns before any delta is applied.</summary>
    public const int PreEditSentinel = 0x45_6E_C0_01;

    /// <summary>The value the generation-one sentinel method returns after the delta is applied.</summary>
    public const int PostEditSentinel = 0x45_6E_C0_02;

    /// <summary>The value the added-static profile stores through the edit-added accessor.</summary>
    public const int AddedStaticValue = 0x45_6E_C0_03;

    /// <summary>The simple file name of the payload baseline assembly.</summary>
    public const string BaselineAssemblyFileName = "PhoenixInspect.EncFixtureBaseline.dll";

    private static Assembly? retainedBaselineAssembly;
    private static Assembly? retainedComparatorAssembly;

    /// <summary>Reads and applies the generation-one delta triple inside its own frame.</summary>
    /// <remarks>
    /// The payload arrays are locals of this frame alone, so they become unreachable when it returns and the
    /// pause-time collection removes this process's own copies; a delta copy that still survives into the dump is
    /// therefore referenced by the runtime itself.
    /// </remarks>
    /// <param name="assembly">The loaded payload baseline assembly.</param>
    /// <param name="payloadDirectory">The directory holding the delta blobs.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ApplyGenerationOne(Assembly assembly, string payloadDirectory) =>
        MetadataUpdater.ApplyUpdate(
            assembly,
            File.ReadAllBytes(Path.Combine(payloadDirectory, "generation-1.metadata-delta")),
            File.ReadAllBytes(Path.Combine(payloadDirectory, "generation-1.il-delta")),
            File.ReadAllBytes(Path.Combine(payloadDirectory, "generation-1.pdb-delta")));

    /// <summary>Loads the payload baseline, applies generation one, verifies the edit, and pauses.</summary>
    /// <param name="profile">The predeclared truth-gate profile.</param>
    /// <param name="payloadDirectory">The directory holding the baseline assembly and delta blobs.</param>
    /// <returns>A process exit code if the target does not remain paused.</returns>
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    public static int Run(string profile, string payloadDirectory)
    {
        if (!MetadataUpdater.IsSupported)
        {
            Console.WriteLine("ENC_NOT_SUPPORTED");
            Console.Out.Flush();
            return 93;
        }

        var assembly = Assembly.LoadFrom(Path.Combine(payloadDirectory, BaselineAssemblyFileName));
        retainedBaselineAssembly = assembly;

        // The comparator is loaded under the same modifiable-assemblies gate and never edited, and its sentinel is
        // invoked once so its module is the edit-enabled, used, but unedited datapoint: a runtime structure that
        // still differs from it on the edited module differs because of the edit, not because of use.
        retainedComparatorAssembly = Assembly.LoadFrom(
            Path.Combine(payloadDirectory, "PhoenixInspect.EncFixtureUnedited.dll"));
        var comparatorSentinel = retainedComparatorAssembly
            .GetType("PhoenixInspect.EncFixtureBaseline.Probe", throwOnError: true)!
            .GetMethod("Sentinel", BindingFlags.Public | BindingFlags.Static)!;
        if ((int)comparatorSentinel.Invoke(null, null)! != PreEditSentinel)
        {
            Console.WriteLine("ENC_COMPARATOR_MISMATCH");
            Console.Out.Flush();
            return 98;
        }
        var probe = assembly.GetType("PhoenixInspect.EncFixtureBaseline.Probe", throwOnError: true)!;
        var sentinel = probe.GetMethod("Sentinel", BindingFlags.Public | BindingFlags.Static)!;

        var preEdit = (int)sentinel.Invoke(null, null)!;
        if (preEdit != PreEditSentinel)
        {
            Console.WriteLine("ENC_BASELINE_MISMATCH");
            Console.Out.Flush();
            return 94;
        }

        ApplyGenerationOne(assembly, payloadDirectory);

        switch (profile)
        {
            case "enc-smoke":
            {
                var postEdit = (int)sentinel.Invoke(null, null)!;
                if (postEdit != PostEditSentinel)
                {
                    Console.WriteLine("ENC_DELTA_NOT_OBSERVED");
                    Console.Out.Flush();
                    return 95;
                }

                return EncPause.WaitForDump(profile, postEdit);
            }

            case "enc-added-static":
            {
                // The added members are resolved on the same loaded type after the edit, stored through the added
                // setter, and read back through the added getter, so readiness proves the edit-added static slot
                // physically exists and holds the stored value in this process.
                var setAdded = probe.GetMethod("SetAdded", BindingFlags.Public | BindingFlags.Static);
                var getAdded = probe.GetMethod("GetAdded", BindingFlags.Public | BindingFlags.Static);
                if (setAdded is null || getAdded is null)
                {
                    Console.WriteLine("ENC_ADDED_MEMBER_NOT_OBSERVED");
                    Console.Out.Flush();
                    return 96;
                }

                setAdded.Invoke(null, [AddedStaticValue]);
                if ((int)getAdded.Invoke(null, null)! != AddedStaticValue)
                {
                    Console.WriteLine("ENC_ADDED_STATIC_NOT_OBSERVED");
                    Console.Out.Flush();
                    return 97;
                }

                return EncPause.WaitForDump(profile, AddedStaticValue);
            }

            default:
                return 92;
        }
    }
}
