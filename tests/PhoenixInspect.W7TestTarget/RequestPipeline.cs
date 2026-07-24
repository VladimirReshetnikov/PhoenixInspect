using System.Runtime.CompilerServices;
using PhoenixInspect.W7TestTarget;

namespace PhoenixInspect.W7TestTarget.Request;

internal static class RequestIncident
{
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    internal static int Run(string incidentId)
    {
        RequestStatics.TotalRequests = StaticValues.Counter;
        RequestStatics.CurrentRequest = incidentId == "request-exact-null-reference"
            ? null
            : new RequestState(
                Id: $"request-{StaticValues.Counter:X8}",
                Status: incidentId == "request-pdb-identity-conflict" ? "conflicting-symbols" : "running");

        IncidentPause.WaitForDump(incidentId);
        GC.KeepAlive(RequestStatics.CurrentRequest);
        return 0;
    }
}

internal static class RequestStatics
{
    internal static int TotalRequests;

    internal static RequestState? CurrentRequest;
}

internal static class UninitializedRequestStatics
{
#pragma warning disable CS0649 // Intentionally never initialized: W7 distinguishes an absent initialized slot from null.
    internal static RequestState? Pending;
#pragma warning restore CS0649
}

internal sealed record RequestState(string Id, string Status);
