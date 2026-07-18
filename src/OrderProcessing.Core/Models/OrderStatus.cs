namespace OrderProcessing.Core.Models;

/// <summary>
/// Represents the current state of an order in the processing pipeline.
/// </summary>
public enum OrderStatus
{
    /// <summary>Order has been created but not yet processed.</summary>
    Pending = 0,

    /// <summary>Order is currently being validated and processed.</summary>
    Processing = 1,

    /// <summary>Order has been successfully processed.</summary>
    Processed = 2,

    /// <summary>Order processing failed due to validation or system error.</summary>
    Failed = 3
}
