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

    internal sealed class CoordinatorRoot(
        CoordinatorOwner? OwnerValue,
        CoordinatorJob? ActiveJobValue,
        CoordinatorWorker[] WorkersValue)
    {
        internal CoordinatorOwner? Owner = OwnerValue;

        internal CoordinatorJob? ActiveJob = ActiveJobValue;

        internal CoordinatorWorker[] Workers = WorkersValue;
    }

    internal sealed class CoordinatorOwner(string NameValue, string RegionValue)
    {
        internal string Name = NameValue;

        internal string Region = RegionValue;
    }

    internal sealed class CoordinatorJob(string IdValue, int RetryCountValue)
    {
        internal string Id = IdValue;

        internal int RetryCount = RetryCountValue;
    }

    internal sealed class CoordinatorWorker(string NodeValue, string StateValue, int QueueDepthValue)
    {
        internal string Node = NodeValue;

        internal string State = StateValue;

        internal int QueueDepth = QueueDepthValue;
    }
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
                new Coordinator.CoordinatorJob($"job-{StaticValues.Counter:X8}", 3),
                [
                    new Coordinator.CoordinatorWorker("node-17", "active", 7),
                    new Coordinator.CoordinatorWorker("node-29", "standby", 2),
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
