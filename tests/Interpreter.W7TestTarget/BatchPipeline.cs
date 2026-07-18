using System.Runtime.CompilerServices;
using Interpreter.W7TestTarget.Batch;

namespace Interpreter.W7TestTarget.Batch
{
    internal static class BatchStatics
    {
        internal static BatchRoot Root = new("uninitialized", Array.Empty<BatchPartition>());

        internal static string State = "uninitialized";

        internal static int? Progress;
    }

    internal sealed record BatchRoot(string State, BatchPartition[] Partitions);

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
            BatchStatics.Root = new BatchRoot(
                state,
                [
                    new BatchPartition("primary", CompletedItems: 17, TotalItems: 29),
                    new BatchPartition("secondary", CompletedItems: 11, TotalItems: 31),
                ]);

            IncidentPause.WaitForDump(incidentId);
            GC.KeepAlive(BatchStatics.Root);
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
