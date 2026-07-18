using System.Runtime.CompilerServices;

namespace Interpreter.W7TestTarget.Workflow;

internal static class WorkflowIncident
{
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    internal static int Run(string incidentId)
    {
        var primary = new AssignedWorker("worker-17", "assigned");
        WorkflowStatics.Root = new WorkflowRoot(
            new WorkflowAttempt("running", primary, Sequence: 42),
            primary,
            [
                new WorkflowAttempt("running", primary, Sequence: 42),
                new WorkflowAttempt("queued", new AssignedWorker("worker-29", "standby"), Sequence: 43),
            ]);

        IncidentPause.WaitForDump(incidentId);
        GC.KeepAlive(WorkflowStatics.Root);
        return 0;
    }
}

internal static class WorkflowStatics
{
    internal static WorkflowRoot Root = new(
        new WorkflowAttempt("uninitialized", new AssignedWorker("none", "none"), Sequence: 0),
        new AssignedWorker("none", "none"),
        Array.Empty<WorkflowAttempt>());

    internal static T? GetCurrent<T>() where T : class => Root.CurrentAttempt as T;
}

internal sealed record WorkflowRoot(
    WorkflowAttempt CurrentAttempt,
    AssignedWorker AssignedWorker,
    WorkflowAttempt[] Attempts);

internal sealed record WorkflowAttempt(string Status, AssignedWorker Worker, int Sequence);

internal sealed record AssignedWorker(string Node, string State);
