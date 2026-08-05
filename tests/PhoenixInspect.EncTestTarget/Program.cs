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

    /// <summary>The simple file name of the payload baseline assembly.</summary>
    public const string BaselineAssemblyFileName = "PhoenixInspect.EncFixtureBaseline.dll";

    private static Assembly? retainedBaselineAssembly;

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
        var probe = assembly.GetType("PhoenixInspect.EncFixtureBaseline.Probe", throwOnError: true)!;
        var sentinel = probe.GetMethod("Sentinel", BindingFlags.Public | BindingFlags.Static)!;

        var preEdit = (int)sentinel.Invoke(null, null)!;
        if (preEdit != PreEditSentinel)
        {
            Console.WriteLine("ENC_BASELINE_MISMATCH");
            Console.Out.Flush();
            return 94;
        }

        MetadataUpdater.ApplyUpdate(
            assembly,
            File.ReadAllBytes(Path.Combine(payloadDirectory, "generation-1.metadata-delta")),
            File.ReadAllBytes(Path.Combine(payloadDirectory, "generation-1.il-delta")),
            File.ReadAllBytes(Path.Combine(payloadDirectory, "generation-1.pdb-delta")));

        var postEdit = (int)sentinel.Invoke(null, null)!;
        if (postEdit != PostEditSentinel)
        {
            Console.WriteLine("ENC_DELTA_NOT_OBSERVED");
            Console.Out.Flush();
            return 95;
        }

        return profile switch
        {
            "enc-smoke" => EncPause.WaitForDump(profile, postEdit),
            _ => 92,
        };
    }
}
