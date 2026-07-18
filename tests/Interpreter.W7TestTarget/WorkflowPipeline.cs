using System.Runtime.CompilerServices;
using Interpreter.W7TestTarget.Batch;

namespace Interpreter.W7TestTarget.Workflow;

internal static class WorkflowIncident
{
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    internal static int Run(string incidentId)
    {
        var primary = new AssignedWorker("worker-17", "assigned");
        var importedRoot = BatchStatics.Root;
        WorkflowStatics.Root = new WorkflowRoot(
            new WorkflowAttempt("running", primary, 42),
            primary,
            [
                new WorkflowAttempt("running", primary, 42),
                new WorkflowAttempt("queued", new AssignedWorker("worker-29", "standby"), 43),
            ]);

        IncidentPause.WaitForDump(incidentId);
        GC.KeepAlive(importedRoot);
        GC.KeepAlive(WorkflowStatics.Root);
        return 0;
    }
}

internal static class WorkflowStatics
{
    internal static WorkflowRoot Root = new(
        new WorkflowAttempt("uninitialized", new AssignedWorker("none", "none"), 0),
        new AssignedWorker("none", "none"),
        Array.Empty<WorkflowAttempt>());

    internal static T? GetCurrent<T>() where T : class => Root.CurrentAttempt as T;
}

internal sealed class WorkflowRoot(
    WorkflowAttempt CurrentAttemptValue,
    AssignedWorker AssignedWorkerValue,
    WorkflowAttempt[] AttemptsValue)
{
    internal WorkflowAttempt CurrentAttempt = CurrentAttemptValue;

    internal AssignedWorker AssignedWorker = AssignedWorkerValue;

    internal WorkflowAttempt[] Attempts = AttemptsValue;
}

internal sealed class WorkflowAttempt(string StatusValue, AssignedWorker WorkerValue, int SequenceValue)
{
    internal string Status = StatusValue;

    internal AssignedWorker Worker = WorkerValue;

    internal int Sequence = SequenceValue;
}

internal sealed class AssignedWorker(string NodeValue, string StateValue)
{
    internal string Node = NodeValue;

    internal string State = StateValue;
}
