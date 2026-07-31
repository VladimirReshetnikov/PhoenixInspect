using Contoso.OrderService.Fulfillment;

namespace Contoso.OrderService.Diagnostics;

/// <summary>
/// The service's process-wide diagnostic state.
/// </summary>
/// <remarks>
/// Ordinary static fields like these are how most services leave a usable trail in a dump: they are reachable
/// without a stack, without a debugger, and without knowing which thread was doing what. They are also the entry
/// point PhoenixInspect binds first, so an inspection can start from a name the reader already knows.
/// </remarks>
public static class ServiceState
{
    /// <summary>The number of orders this process dispatched before it stalled.</summary>
    public static int ProcessedOrderCount;

    /// <summary>The build this process is running.</summary>
    public static string BuildLabel = "unset";

    /// <summary>The failure code of the last dispatch attempt, or null when no attempt has failed.</summary>
    public static int? LastFailureCode;

    /// <summary>The note an operator attached to this process, or null when nobody attached one.</summary>
    public static string? OperatorNote;

    /// <summary>The dispatcher this process is draining.</summary>
    public static ShipmentDispatcher? Dispatcher;
}
