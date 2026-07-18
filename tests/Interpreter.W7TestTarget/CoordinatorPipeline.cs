using System.Runtime.CompilerServices;
using CoordinatorValues = Interpreter.W7TestTarget.Coordinator.CoordinatorStatics;

namespace Interpreter.W7TestTarget.Coordinator
{
    internal static class CoordinatorStatics
    {
        internal static CoordinatorRoot Root = new(
            new CoordinatorOwner("uninitialized", "none"),
            null,
            Array.Empty<CoordinatorWorker>());
    }

    internal sealed record CoordinatorRoot(
        CoordinatorOwner? Owner,
        CoordinatorJob? ActiveJob,
        CoordinatorWorker[] Workers);

    internal sealed record CoordinatorOwner(string Name, string Region);

    internal sealed record CoordinatorJob(string Id, int RetryCount);

    internal sealed record CoordinatorWorker(string Node, string State, int QueueDepth);
}

namespace Interpreter.W7TestTarget.CoordinatorContext
{
    internal static class CoordinatorIncident
    {
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        internal static int Run(string incidentId)
        {
            CoordinatorValues.Root = new Coordinator.CoordinatorRoot(
                new Coordinator.CoordinatorOwner("coordinator-west", "west"),
                new Coordinator.CoordinatorJob($"job-{StaticValues.Counter:X8}", RetryCount: 3),
                [
                    new Coordinator.CoordinatorWorker("node-17", "active", QueueDepth: 7),
                    new Coordinator.CoordinatorWorker("node-29", "standby", QueueDepth: 2),
                ]);

            IncidentPause.WaitForDump(incidentId);
            GC.KeepAlive(CoordinatorValues.Root);
            return 0;
        }
    }
}

namespace Interpreter.W7TestTarget.Coordinator
{
    internal static class CoordinatorIncident
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static int Run(string incidentId) => CoordinatorContext.CoordinatorIncident.Run(incidentId);
    }
}
