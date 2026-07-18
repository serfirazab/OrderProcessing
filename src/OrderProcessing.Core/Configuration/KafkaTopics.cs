namespace OrderProcessing.Core.Configuration;

/// <summary>
/// Kafka topic name constants for the order processing pipeline.
/// </summary>
public static class KafkaTopics
{
    /// <summary>Topic for newly created raw (unprocessed) orders.</summary>
    public const string RawOrders = "raw-orders";

    /// <summary>Topic for orders that have been validated and processed.</summary>
    public const string ProcessedOrders = "processed-orders";

    /// <summary>Topic for order status updates (used to sync state).</summary>
    public const string OrderStatus = "order-status";
}
