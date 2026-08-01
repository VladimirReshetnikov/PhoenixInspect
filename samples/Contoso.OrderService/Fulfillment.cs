namespace Contoso.OrderService.Fulfillment;

/// <summary>One carrier the service can hand a batch to.</summary>
public sealed class Carrier
{
    /// <summary>Creates a carrier.</summary>
    /// <param name="name">The carrier's display name.</param>
    /// <param name="serviceLevel">The contracted service level.</param>
    public Carrier(string name, string serviceLevel)
    {
        Name = name;
        ServiceLevel = serviceLevel;
    }

    /// <summary>The carrier's display name.</summary>
    public string Name;

    /// <summary>The contracted service level.</summary>
    public string ServiceLevel;
}

/// <summary>The reason a dispatch attempt stopped.</summary>
public sealed class FailureRecord
{
    /// <summary>Creates a failure record.</summary>
    /// <param name="code">The stable failure code.</param>
    /// <param name="detail">The operator-facing detail.</param>
    /// <param name="attempt">The one-based attempt number that failed.</param>
    public FailureRecord(string code, string detail, int attempt)
    {
        Code = code;
        Detail = detail;
        Attempt = attempt;
    }

    /// <summary>The stable failure code.</summary>
    public string Code;

    /// <summary>The operator-facing detail.</summary>
    public string Detail;

    /// <summary>The one-based attempt number that failed.</summary>
    public int Attempt;
}

/// <summary>One transit corridor a route runs through.</summary>
public sealed class TransitCorridor
{
    /// <summary>Creates a corridor.</summary>
    /// <param name="name">The corridor's display name.</param>
    /// <param name="segmentCount">The number of road segments the corridor spans.</param>
    public TransitCorridor(string name, int segmentCount)
    {
        Name = name;
        SegmentCount = segmentCount;
    }

    /// <summary>The corridor's display name.</summary>
    public string Name;

    /// <summary>The number of road segments the corridor spans.</summary>
    public int SegmentCount;
}

/// <summary>The route a batch was planned onto.</summary>
public sealed class RouteAssignment
{
    /// <summary>Creates a route assignment.</summary>
    /// <param name="hubCode">The exact dock code inside the destination hub.</param>
    /// <param name="legCount">The number of legs the route has.</param>
    /// <param name="corridor">The corridor the longest leg runs through.</param>
    public RouteAssignment(string hubCode, int legCount, TransitCorridor corridor)
    {
        HubCode = hubCode;
        LegCount = legCount;
        Corridor = corridor;
    }

    /// <summary>The exact dock code inside the destination hub.</summary>
    public string HubCode;

    /// <summary>The number of legs the route has.</summary>
    public int LegCount;

    /// <summary>The corridor the longest leg runs through.</summary>
    public TransitCorridor Corridor;
}

/// <summary>A supervisor's note attached to an escalation.</summary>
public sealed class ReviewNote
{
    /// <summary>Creates a review note.</summary>
    /// <param name="owner">The supervisor who owns the escalation.</param>
    public ReviewNote(string owner) => Owner = owner;

    /// <summary>The supervisor who owns the escalation.</summary>
    public string Owner;
}

/// <summary>An operator escalation opened against a batch.</summary>
public sealed class EscalationRecord
{
    /// <summary>Creates an escalation record.</summary>
    /// <param name="reason">The reason the batch was escalated.</param>
    /// <param name="review">The supervisor review attached to the escalation.</param>
    public EscalationRecord(string reason, ReviewNote review)
    {
        Reason = reason;
        Review = review;
    }

    /// <summary>The reason the batch was escalated.</summary>
    public string Reason;

    /// <summary>The supervisor review attached to the escalation.</summary>
    public ReviewNote Review;
}

/// <summary>One batch of orders moving through dispatch.</summary>
public sealed class OrderBatch
{
    /// <summary>Creates an order batch.</summary>
    /// <param name="batchId">The batch identifier.</param>
    /// <param name="pendingCount">The number of orders still awaiting dispatch.</param>
    /// <param name="destinationHub">The hub the batch is routed to.</param>
    /// <param name="route">The route the batch was planned onto.</param>
    public OrderBatch(string batchId, int pendingCount, string destinationHub, RouteAssignment route)
    {
        BatchId = batchId;
        PendingCount = pendingCount;
        DestinationHub = destinationHub;
        Route = route;
    }

    /// <summary>The batch identifier.</summary>
    public string BatchId;

    /// <summary>The number of orders still awaiting dispatch.</summary>
    public int PendingCount;

    /// <summary>The hub the batch is routed to.</summary>
    public string DestinationHub;

    /// <summary>The route the batch was planned onto.</summary>
    public RouteAssignment Route;

    /// <summary>The escalation opened against this batch, or null while nobody escalated it.</summary>
    public EscalationRecord? Escalation;

    /// <summary>The handling tags operators attached to this batch.</summary>
    public string[] Tags = ["priority", "cross-border", "temp-controlled"];
}

/// <summary>The dispatcher that drains a queue of batches to carriers.</summary>
public sealed class ShipmentDispatcher
{
    /// <summary>Creates a dispatcher.</summary>
    /// <param name="region">The region this dispatcher serves.</param>
    /// <param name="queueDepth">The number of batches waiting behind the current one.</param>
    /// <param name="currentBatch">The batch currently being dispatched.</param>
    public ShipmentDispatcher(string region, int queueDepth, OrderBatch currentBatch)
    {
        Region = region;
        QueueDepth = queueDepth;
        CurrentBatch = currentBatch;
    }

    /// <summary>The region this dispatcher serves.</summary>
    public string Region;

    /// <summary>The number of batches waiting behind the current one.</summary>
    public int QueueDepth;

    /// <summary>The batch currently being dispatched.</summary>
    public OrderBatch CurrentBatch;

    /// <summary>The carrier the current batch was handed to, or null while no carrier is assigned.</summary>
    public Carrier? AssignedCarrier;

    /// <summary>The reason the last dispatch attempt stopped, or null when the last attempt succeeded.</summary>
    public FailureRecord? LastFailure;

    /// <summary>Durations of the most recent dispatch attempts, in milliseconds; the outlier is the stall.</summary>
    public int[] RecentDispatchDurationsMs = [1210, 980, 30045, 1105];
}
