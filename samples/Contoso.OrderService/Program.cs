using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Contoso.OrderService.Diagnostics;
using Contoso.OrderService.Dispatching;
using Contoso.OrderService.Fulfillment;

namespace Contoso.OrderService;

/// <summary>
/// Entry point of the PhoenixInspect preview demo target.
/// </summary>
/// <remarks>
/// The process reaches one deterministic stalled state — a batch that no carrier ever accepted — then prints
/// <c>READY</c> and parks. A full dump taken at that moment contains exactly the evidence the demo asks about, and
/// every value is fixed, so the same expressions produce the same answers on any machine.
/// </remarks>
public static class Program
{
    private static int Main(string[] args)
    {
        ServiceState.ProcessedOrderCount = 84_213;
        ServiceState.BuildLabel = "2026.07.30-preview";
        ServiceState.LastFailureCode = 5031;
        ServiceState.OperatorNote = null;

        var dispatcher = new ShipmentDispatcher(
            region: "eu-west-1",
            queueDepth: 17,
            currentBatch: new OrderBatch(
                batchId: "batch-2026-07-30-0042",
                pendingCount: 96,
                destinationHub: "AMS-3"))
        {
            // No carrier ever accepted the hand-off, which is why the batch is still pending.
            AssignedCarrier = null,
            LastFailure = new FailureRecord(
                code: "carrier-handoff-timeout",
                detail: "No carrier accepted batch-2026-07-30-0042 within the 30s hand-off window.",
                attempt: 4),
        };
        ServiceState.Dispatcher = dispatcher;

        var rooted = GCHandle.Alloc(dispatcher, GCHandleType.Normal);
        CarrierGateway.StartPollingWorkers(dispatcher, workerCount: 2);

        var exitCode = ShipmentDispatcher_Drain(dispatcher, args);
        rooted.Free();
        return exitCode;
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    private static int ShipmentDispatcher_Drain(ShipmentDispatcher dispatcher, string[] args)
    {
        GC.KeepAlive(args);
        return DispatchLoop.DrainQueue(dispatcher);
    }
}
