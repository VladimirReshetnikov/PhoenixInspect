using System.Runtime.CompilerServices;
using static Interpreter.W8RequestShapeTarget.RequestImports<Interpreter.W8RequestShapeTarget.RequestContext>;

namespace Interpreter.W8RequestShapeTarget;

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

        return RequestShapeGate.Run(profile);
    }
}

/// <summary>Holds the sole pause the request-shape target uses to signal dump readiness.</summary>
/// <remarks>This is a draft target entry point and not a product API.</remarks>
public static class RequestPause
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

/// <summary>Materializes the request-shape object graph and enters the selected truth-gate profile.</summary>
/// <remarks>This is a draft target entry point and not a product API.</remarks>
public static class RequestShapeGate
{
    /// <summary>Initializes the request-shape physical fixture and enters the selected profile.</summary>
    /// <param name="profile">The predeclared truth-gate profile of exactly one corpus incident.</param>
    /// <returns>A process exit code if the target does not remain paused.</returns>
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    public static int Run(string profile)
    {
        var request = new RequestContext("request-shape-101");
        var batch = new BatchContext("request-shape-batch-103");

        RequestSlot<RequestContext>.Sentinel = 0x51_00_01_01;
        RequestSlot<RequestContext>.Current = request;
        RequestSlot<RequestContext>.ThreadSentinel = 0x51_00_01_22;
        RequestSlot<BatchContext>.Sentinel = 0x51_00_01_02;
        RequestSlot<BatchContext>.Current = batch;

        RequestImports<RequestContext>.ImportedSentinel = 0x51_00_01_0D;
        RequestImports<RequestContext>.ShadowedSentinel = 0x51_00_01_1D;
        OuterScopedSlot.Sentinel = 0x51_00_01_08;
        Inner.InnerScopedSlot.Sentinel = 0x51_00_01_09;

        return profile switch
        {
            "request-qualified-sentinel" => RequestPause.WaitForDump(profile, RequestSlot<RequestContext>.Sentinel),
            "request-distinct-constructions" => RequestPause.WaitForDump(profile, RequestSlot<BatchContext>.Sentinel),
            "request-alias-shadowing" => Inner.InnerAliasProbe.Run(profile),
            "request-using-static-bare" => BareImportProbe.Run(profile),
            "request-frameless-alias" => Outer.OuterAliasProbe.Run(profile),
            "request-malformed-typespec" => RequestPause.WaitForDump(profile, RequestSlot<RequestContext>.Sentinel),
            "request-absent-construction" => RequestPause.WaitForDump(profile, RequestSlot<RequestContext>.Sentinel),
            "request-local-shadow" => LocalShadowProbe.Run(profile),
            "request-thread-relative" => RequestThreadWorker.Run(profile),
            _ => 92,
        };
    }
}

/// <summary>Parks one alternative worker thread with its own thread-relative sentinel before the main pause.</summary>
/// <remarks>This is a draft frame probe and not a frame-value product contract.</remarks>
public static class RequestThreadWorker
{
    /// <summary>Stores the distinct sentinel the parked worker writes into its own thread-relative slot.</summary>
    public const int WorkerSentinel = 0x51_00_01_23;

    private static readonly ManualResetEventSlim WorkerParked = new(initialState: false);

    /// <summary>Starts the identifiable parked worker and then pauses the main truth-gate thread.</summary>
    /// <param name="profile">The selected truth-gate profile.</param>
    /// <returns>A process exit code if the pause unexpectedly returns.</returns>
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    public static int Run(string profile)
    {
        var worker = new Thread(static () => Park(WorkerSentinel))
        {
            IsBackground = true,
            Name = "w8-request-thread-relative-worker",
        };
        worker.Start();
        WorkerParked.Wait();
        return RequestPause.WaitForDump(profile, RequestSlot<RequestContext>.ThreadSentinel);
    }

    /// <summary>Writes the worker's own thread-relative sentinel and parks inside this identifiable frame.</summary>
    /// <param name="workerSentinel">The distinct worker sentinel value.</param>
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    private static void Park(int workerSentinel)
    {
        RequestSlot<RequestContext>.ThreadSentinel = workerSentinel;
        WorkerParked.Set();
        Thread.Sleep(Timeout.Infinite);
        GC.KeepAlive(workerSentinel);
    }
}

/// <summary>Pauses where the bare imported spelling has no higher-precedence blocker.</summary>
/// <remarks>This is a draft frame probe and not a frame-value product contract.</remarks>
public static class BareImportProbe
{
    /// <summary>Reads the bare imported sentinel and pauses for the full dump.</summary>
    /// <param name="profile">The selected truth-gate profile.</param>
    /// <returns>A process exit code if the pause unexpectedly returns.</returns>
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    public static int Run(string profile) => RequestPause.WaitForDump(profile, ImportedSentinel);
}

/// <summary>Pauses where an active local shadows the identically spelled imported static field.</summary>
/// <remarks>This is a draft frame probe and not a frame-value product contract.</remarks>
public static class LocalShadowProbe
{
    /// <summary>Declares the shadowing local and pauses for the full dump.</summary>
    /// <param name="profile">The selected truth-gate profile.</param>
    /// <returns>A process exit code if the pause unexpectedly returns.</returns>
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    public static int Run(string profile)
    {
        var ShadowedSentinel = 0x51_00_01_1E;
        return RequestPause.WaitForDump(profile, ShadowedSentinel);
    }
}
