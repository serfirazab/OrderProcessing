using Confluent.Kafka;
using OrderProcessing.Core.Configuration;
using OrderProcessing.Core.Models;
using System.Text.Json;

namespace OrderProcessing.LoggingConsumer;

/// <summary>
/// Terminal consumer that logs all processed orders for auditing/monitoring.
/// This is a "dead-end" consumer — it does not produce to any further topic.
/// </summary>
public class LoggingConsumerWorker : BackgroundService
{
    private readonly ILogger<LoggingConsumerWorker> _logger;
    private readonly string _bootstrapServers;

    public LoggingConsumerWorker(ILogger<LoggingConsumerWorker> logger, IConfiguration config)
    {
        _logger = logger;
        _bootstrapServers = config.GetValue<string>("Kafka:BootstrapServers") ?? "localhost:9092";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("LoggingConsumerWorker started, waiting for processed orders...");

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = _bootstrapServers,
            GroupId = "logging-consumer-group",
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

                    if (order is null)
                    {
                        consumer.Commit(result);
                        continue;
                    }

                    // Structured audit log for the processed order
                    _logger.LogInformation(
                        "┌─────────────────────────────────────────────");
                    _logger.LogInformation(
                        "│ 📋 AUDIT LOG — Order {OrderId}", order.Id);
                    _logger.LogInformation(
                        "│ Customer: {Name} <{Email}>", order.CustomerName, order.CustomerEmail);
                    _logger.LogInformation(
                        "│ Total: {Total:C}", order.TotalPrice);
                    _logger.LogInformation(
                        "│ Status: {Status}", order.Status);
                    _logger.LogInformation(
                        "│ Items: {Count}", order.Items.Count);

                    foreach (var item in order.Items)
                    {
                        _logger.LogInformation(
                            "│   • {Product} × {Qty} = {Sub:C}",
                            item.ProductName, item.Quantity, item.SubTotal);
                    }

                    _logger.LogInformation(
                        "│ Created: {Time:yyyy-MM-dd HH:mm:ss} UTC", order.CreatedAt);
                    _logger.LogInformation(
                        "└─────────────────────────────────────────────");

                    await Task.Delay(100, stoppingToken);
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
            _logger.LogInformation("LoggingConsumerWorker shutting down gracefully");
        }
        finally
        {
            consumer.Close();
        }
    }
}
