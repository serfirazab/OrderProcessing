using Confluent.Kafka;
using OrderProcessing.Core.Configuration;
using OrderProcessing.Core.Models;
using System.Text.Json;

namespace OrderProcessing.API.Services;

/// <summary>
/// Publishes orders to Kafka topics.
/// Uses JSON serialization (System.Text.Json) for message format.
/// </summary>
public class OrderPublisherService
{
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<OrderPublisherService> _logger;

    public OrderPublisherService(string bootstrapServers, ILogger<OrderPublisherService> logger)
    {
        _producer = new ProducerBuilder<string, string>(
            new ProducerConfig
            {
                BootstrapServers = bootstrapServers,
                Acks = Acks.All,
                EnableIdempotence = true,
                MessageSendMaxRetries = 3,
                RetryBackoffMs = 1000
            }).Build();

        _logger = logger;
    }

    /// <summary>
    /// Publishes a new order to the raw-orders topic for async processing.
    /// Uses the order ID as the Kafka message key for idempotent delivery.
    /// </summary>
    public async Task PublishOrderAsync(Order order)
    {
        var json = JsonSerializer.Serialize(order, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });

        var message = new Message<string, string>
        {
            Key = order.Id.ToString(),
            Value = json,
            Headers = new Headers
            {
                new Header("message-type", System.Text.Encoding.UTF8.GetBytes("order.created")),
                new Header("content-type", System.Text.Encoding.UTF8.GetBytes("application/json"))
            }
        };

        var result = await _producer.ProduceAsync(KafkaTopics.RawOrders, message);

        _logger.LogInformation(
            "Published order {OrderId} to Kafka at offset {Offset}",
            order.Id, result.TopicPartitionOffset);
    }

    public void Dispose()
    {
        _producer?.Dispose();
    }
}
