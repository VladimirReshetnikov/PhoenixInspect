using System.Runtime.CompilerServices;
using Contoso.OrderService.Fulfillment;

namespace Contoso.OrderService.Dispatching;

/// <summary>Drains the dispatcher's queue until every batch has a carrier.</summary>
public static class DispatchLoop
{
    /// <summary>Runs the drain loop and parks once the current batch has stalled.</summary>
    /// <param name="dispatcher">The dispatcher being drained.</param>
    /// <returns>The process exit code; the demo target never reaches a normal return.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="dispatcher"/> is null.</exception>
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    public static int DrainQueue(ShipmentDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        return AwaitCarrierAssignment(dispatcher);
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    private static int AwaitCarrierAssignment(ShipmentDispatcher dispatcher)
    {
        // The demo target stalls here on purpose: this is the frame a dump would be taken at.
        Console.WriteLine("READY");
        Console.Out.Flush();
        Thread.Sleep(Timeout.Infinite);
        GC.KeepAlive(dispatcher);
        return 0;
    }
}

/// <summary>Background workers that poll an external carrier gateway for an assignment.</summary>
public static class CarrierGateway
{
    /// <summary>Starts the requested number of polling workers and returns once they are parked.</summary>
    /// <param name="dispatcher">The dispatcher whose batch the workers are polling for.</param>
    /// <param name="workerCount">The number of workers to start.</param>
    /// <exception cref="ArgumentNullException"><paramref name="dispatcher"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="workerCount"/> is negative.</exception>
    public static void StartPollingWorkers(ShipmentDispatcher dispatcher, int workerCount)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentOutOfRangeException.ThrowIfNegative(workerCount);
        using var parked = new CountdownEvent(workerCount);
        for (var ordinal = 0; ordinal < workerCount; ordinal++)
        {
            var worker = new Thread(() => PollForAssignment(dispatcher, parked))
            {
                IsBackground = true,
                Name = $"carrier-poll-{ordinal}",
            };
            worker.Start();
        }

        parked.Wait();
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    private static void PollForAssignment(ShipmentDispatcher dispatcher, CountdownEvent parked)
    {
        parked.Signal();
        Thread.Sleep(Timeout.Infinite);
        GC.KeepAlive(dispatcher);
    }
}
