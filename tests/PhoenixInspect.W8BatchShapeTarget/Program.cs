using System.Runtime.CompilerServices;
using BatchLedgerAlias = PhoenixInspect.W8BatchShapeTarget.Ledger<PhoenixInspect.W8BatchShapeTarget.BatchKey>;
using NamedRvaStorage = PhoenixInspect.W8NamedRvaTarget.NamedRvaStorage;

namespace PhoenixInspect.W8BatchShapeTarget;

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

        return BatchShapeGate.Run(profile);
    }
}

/// <summary>Holds the sole pause the batch-shape target uses to signal dump readiness.</summary>
/// <remarks>This is a draft target entry point and not a product API.</remarks>
public static class BatchPause
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

/// <summary>Materializes the batch-shape object graph and enters the selected truth-gate profile.</summary>
/// <remarks>This is a draft target entry point and not a product API.</remarks>
public static class BatchShapeGate
{
    /// <summary>Initializes the batch-shape physical fixture and enters the selected profile.</summary>
    /// <param name="profile">The predeclared truth-gate profile of exactly one corpus incident.</param>
    /// <returns>A process exit code if the target does not remain paused.</returns>
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    public static int Run(string profile)
    {
        var key = new BatchKey("batch-shape-key-211");
        var value = new BatchValue("batch-shape-value-223");

        BatchLedgerAlias.Count = 0x61_00_02_01;
        Ledger<BatchValue>.Count = 0x61_00_02_02;
        Ledger<Wrap<BatchKey>>.Count = 0x61_00_02_03;
        Ledger<Wrap<Wrap<BatchKey>>>.Count = 0x61_00_02_04;
        Ledger<Wrap<Wrap<Wrap<BatchKey>>>>.Count = 0x61_00_02_05;
        Ledger<Wrap<Wrap<Wrap<Wrap<BatchKey>>>>>.Count = 0x61_00_02_06;

        Outer<BatchKey>.Inner<BatchValue>.Count = 0x61_00_02_05;
        Outer<BatchKey>.Inner<BatchValue>.Current = value;
        Outer<BatchValue>.Inner<BatchKey>.Count = 0x61_00_02_06;

        BatchImports.ImportedNestedCurrent = new ImportedNested("batch-nested-label");
        BatchImports.PendingCount = 0x61_00_02_1E;

        PhoenixInspect.W8AliasTarget.ExternalSlot<PhoenixInspect.W8AliasTarget.ForwardedRequestContext>.Sentinel =
            0x61_00_02_09;
        PhoenixInspect.W8AliasTarget.ExternalSlot<PhoenixInspect.W8AliasTarget.ForwardedRequestContext>.Current =
            new PhoenixInspect.W8AliasTarget.ForwardedRequestContext("batch-forwarded-227");

        var namedRvaSentinel = NamedRvaStorage.NamedSentinel;
        GC.KeepAlive(key);

        return profile switch
        {
            "batch-typespec-alias" => BatchPause.WaitForDump(profile, BatchLedgerAlias.Count),
            "batch-nested-arity" => BatchPause.WaitForDump(profile, Outer<BatchKey>.Inner<BatchValue>.Count),
            "batch-forwarder-convergence" => ConvergenceProbe.Run(profile),
            "batch-using-static-nested" => NestedHeadProbe.Run(profile),
            "batch-partial-pdb" => BatchPause.WaitForDump(profile, BatchLedgerAlias.Count),
            "batch-typespec-depth" => BatchPause.WaitForDump(profile, Ledger<Wrap<Wrap<Wrap<Wrap<BatchKey>>>>>.Count),
            "batch-candidate-cap" => BatchPause.WaitForDump(profile, Ledger<BatchValue>.Count),
            "batch-incomplete-catalogs" => PendingCountProbe.Run(profile),
            "batch-module-rva" => BatchPause.WaitForDump(profile, namedRvaSentinel),
            _ => 92,
        };
    }
}
