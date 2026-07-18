using System.Runtime.CompilerServices;
using Interpreter.W7TestTarget.Batch;

namespace Interpreter.W7TestTarget.Batch
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

    internal sealed record BatchRoot(string State, BatchPartition[] Partitions) : IBatchRoot
    {
        internal int Marker = 0x1837A24C;

        internal BatchSummary Summary = new(State, CompletedItems: 28, TotalItems: 60);
    }

    internal sealed record BatchSummary(string State, int CompletedItems, int TotalItems)
    {
        internal string DisplayState = State;

        internal int Completed = CompletedItems;
    }

    internal sealed record BatchPartition(string Name, int CompletedItems, int TotalItems);
}

namespace Interpreter.W7TestTarget.BatchContext
{
    internal static class BatchIncident
    {
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        internal static int Run(string incidentId)
        {
            var state = incidentId == "batch-import-ambiguity" ? "ambiguous-import" : "processing";
            BatchStatics.State = state;
            BatchStatics.Progress = null;
            BatchStatics.TotalItems = StaticValues.Counter;
            BatchStatics.Root = new BatchRoot(
                state,
                [
                    new BatchPartition("primary", CompletedItems: 17, TotalItems: 29),
                    new BatchPartition("secondary", CompletedItems: 11, TotalItems: 31),
                ]);
            BatchStatics.InterfaceRoot = BatchStatics.Root;
            BatchStatics.NumberArray = new[] { 13, 21, 34, 55 };
            BatchStatics.ObjectArray = new[] { "north", "south", "east", "west" };

            IncidentPause.WaitForDump(incidentId);
            GC.KeepAlive(BatchStatics.Root);
            GC.KeepAlive(BatchStatics.InterfaceRoot);
            GC.KeepAlive(BatchStatics.NumberArray);
            GC.KeepAlive(BatchStatics.ObjectArray);
            return 0;
        }
    }
}

namespace Interpreter.W7TestTarget.Batch
{
    internal static class BatchIncident
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static int Run(string incidentId) => BatchContext.BatchIncident.Run(incidentId);
    }
}
