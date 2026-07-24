using System.Runtime.CompilerServices;
using PhoenixInspect.W7TestTarget.Batch;
using PhoenixInspect.W7TestTarget.BatchShadow;
using BatchValues = PhoenixInspect.W7TestTarget.Batch.BatchStatics;

namespace PhoenixInspect.W7TestTarget.Batch
{
    internal static class BatchStatics
    {
        internal static BatchRoot Root = new("uninitialized", Array.Empty<BatchPartition>());

        internal static string State = "uninitialized";

        internal static int? Progress;

        internal static int TotalItems;

        internal static IBatchRoot InterfaceRoot = Root;

        internal static Array NumberArray = Array.Empty<int>();

        internal static object ObjectArray = Array.Empty<string>();
    }

    internal interface IBatchRoot
    {
        string State { get; }
    }

    internal sealed class BatchRoot : IBatchRoot
    {
        internal BatchRoot(string state, BatchPartition[] partitions)
        {
            State = state;
            Partitions = partitions;
            Summary = new BatchSummary(state, CompletedItems: 28, TotalItems: 60);
        }

        internal string State;

        internal BatchPartition[] Partitions;

        internal int Marker = 0x1837A24C;

        internal BatchSummary Summary;

        string IBatchRoot.State => State;
    }

    internal sealed record BatchSummary(string State, int CompletedItems, int TotalItems)
    {
        internal string DisplayState = State;

        internal int Completed = CompletedItems;
    }

    internal sealed record BatchPartition(string Name, int CompletedItems, int TotalItems);
}

namespace PhoenixInspect.W7TestTarget.BatchContext
{
    internal static class BatchIncident
    {
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        internal static int Run(string incidentId)
        {
            var state = incidentId == "batch-import-ambiguity" ? "ambiguous-import" : "processing";
            BatchValues.State = state;
            BatchValues.Progress = null;
            BatchValues.TotalItems = StaticValues.Counter;
            BatchValues.Root = new BatchRoot(
                state,
                [
                    new BatchPartition("primary", CompletedItems: 17, TotalItems: 29),
                    new BatchPartition("secondary", CompletedItems: 11, TotalItems: 31),
                ]);
            BatchValues.InterfaceRoot = BatchValues.Root;
            BatchValues.NumberArray = new[] { 13, 21, 34, 55 };
            BatchValues.ObjectArray = new[] { "north", "south", "east", "west" };

            IncidentPause.WaitForDump(incidentId);
            GC.KeepAlive(BatchValues.Root);
            GC.KeepAlive(BatchValues.InterfaceRoot);
            GC.KeepAlive(BatchValues.NumberArray);
            GC.KeepAlive(BatchValues.ObjectArray);
            return 0;
        }
    }
}

namespace PhoenixInspect.W7TestTarget.BatchShadow
{
    internal static class BatchStatics
    {
        internal static string State = "shadow";
    }
}

namespace PhoenixInspect.W7TestTarget.Batch
{
    internal static class BatchIncident
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static int Run(string incidentId) => BatchContext.BatchIncident.Run(incidentId);
    }
}
