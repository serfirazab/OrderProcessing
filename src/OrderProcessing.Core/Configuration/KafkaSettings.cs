namespace OrderProcessing.Core.Configuration;

/// <summary>
/// Kafka connection and topic configuration settings.
/// Bound from appsettings.json via IOptions pattern.
/// </summary>
public class KafkaSettings
{
    /// <summary>Kafka broker connection string (default: localhost:9092).</summary>
    public string BootstrapServers { get; set; } = "localhost:9092";

    /// <summary>Consumer group ID for the order processor worker.</summary>
    public string OrderProcessorGroupId { get; set; } = "order-processor-group";

    /// <summary>Consumer group ID for the email worker.</summary>
    public string EmailConsumerGroupId { get; set; } = "email-consumer-group";

    /// <summary>Consumer group ID for the stock worker.</summary>
    public string StockConsumerGroupId { get; set; } = "stock-consumer-group";

    /// <summary>Consumer group ID for the logging worker.</summary>
    public string LoggingConsumerGroupId { get; set; } = "logging-consumer-group";

    /// <summary>Topic for newly created raw orders.</summary>
    public string RawOrdersTopic { get; set; } = "raw-orders";

    /// <summary>Topic for fully processed orders.</summary>
    public string ProcessedOrdersTopic { get; set; } = "processed-orders";
}
