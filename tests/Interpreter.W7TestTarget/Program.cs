using System.Runtime.CompilerServices;

namespace Interpreter.W7TestTarget;

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

        if (args is not ["--incident", var incidentId, ..])
        {
            Console.WriteLine("INVALID_ARGUMENTS");
            Console.Out.Flush();
            return 91;
        }

        return incidentId switch
        {
            "request-qualified-scalar" or
            "request-exact-null-reference" or
            "request-pdb-identity-conflict" or
            "request-slot-unavailable" => Request.RequestIncident.Run(incidentId),

            "batch-imported-direct-field" or
            "batch-nullable-no-value" or
            "batch-import-ambiguity" or
            "batch-partial-slot-bytes" => Batch.BatchIncident.Run(incidentId),

            "coordinator-type-alias-owner" or
            "coordinator-frame-unavailable" or
            "coordinator-duplicate-qualified-definition" or
            "coordinator-target-header-conflict" => Coordinator.CoordinatorIncident.Run(incidentId),

            "workflow-current-namespace-chain" or
            "workflow-pdb-partial" or
            "workflow-field-absence-vs-invalid-signature" or
            "workflow-valid-unadmitted-generic-call" => Workflow.WorkflowIncident.Run(incidentId),

            _ => 92,
        };
    }
}

internal static class IncidentPause
{
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    internal static void WaitForDump(string incidentId)
    {
        Console.WriteLine("READY");
        Console.Out.Flush();
        Thread.Sleep(Timeout.Infinite);
        GC.KeepAlive(incidentId);
    }
}
