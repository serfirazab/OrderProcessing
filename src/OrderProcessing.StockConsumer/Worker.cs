using Confluent.Kafka;
using OrderProcessing.Core.Configuration;
using OrderProcessing.Core.Models;
using System.Text.Json;

namespace OrderProcessing.StockConsumer;

/// <summary>
/// Consumes processed orders and simulates inventory/stock updates for each line item.
/// Demonstrates a side-effect consumer that does not produce to another topic.
/// </summary>
public class StockConsumerWorker : BackgroundService
{
    private readonly ILogger<StockConsumerWorker> _logger;
    private readonly string _bootstrapServers;

    public StockConsumerWorker(ILogger<StockConsumerWorker> logger, IConfiguration config)
    {
        _logger = logger;
        _bootstrapServers = config.GetValue<string>("Kafka:BootstrapServers") ?? "localhost:9092";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("StockConsumerWorker started, waiting for processed orders...");

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = _bootstrapServers,
            GroupId = "stock-consumer-group",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
            AllowAutoCreateTopics = true
        };

        using var consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
        consumer.Subscribe(KafkaTopics.ProcessedOrders);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var result = consumer.Consume(stoppingToken);

                    var order = JsonSerializer.Deserialize<Order>(result.Message.Value,
                        new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

                    if (order is null || order.Status != OrderStatus.Processed)
                    {
                        consumer.Commit(result);
                        continue;
                    }

                    _logger.LogInformation(
                        "📦 Updating stock for order {OrderId} ({ItemCount} items)...",
                        order.Id, order.Items.Count);

                    foreach (var item in order.Items)
                    {
                        // Simulate async stock deduction for each product
                        await Task.Delay(200, stoppingToken);
                        _logger.LogInformation(
                            "  Stock deducted: {Product} (Qty: {Qty})",
                            item.ProductName, item.Quantity);
                    }

                    _logger.LogInformation(
                        "✅ Stock updated for order {OrderId}", order.Id);

                    consumer.Commit(result);
                }
                catch (ConsumeException ex) when (!ex.Error.IsFatal)
                {
                    _logger.LogWarning("Kafka consume error: {Reason}", ex.Error.Reason);
                    await Task.Delay(1000, stoppingToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("StockConsumerWorker shutting down gracefully");
        }
        finally
        {
            consumer.Close();
        }
    }
}
