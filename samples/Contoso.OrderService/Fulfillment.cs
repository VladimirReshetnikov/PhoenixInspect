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

/// <summary>One batch of orders moving through dispatch.</summary>
public sealed class OrderBatch
{
    /// <summary>Creates an order batch.</summary>
    /// <param name="batchId">The batch identifier.</param>
    /// <param name="pendingCount">The number of orders still awaiting dispatch.</param>
    /// <param name="destinationHub">The hub the batch is routed to.</param>
    public OrderBatch(string batchId, int pendingCount, string destinationHub)
    {
        BatchId = batchId;
        PendingCount = pendingCount;
        DestinationHub = destinationHub;
    }

    /// <summary>The batch identifier.</summary>
    public string BatchId;

    /// <summary>The number of orders still awaiting dispatch.</summary>
    public int PendingCount;

    /// <summary>The hub the batch is routed to.</summary>
    public string DestinationHub;
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
}
