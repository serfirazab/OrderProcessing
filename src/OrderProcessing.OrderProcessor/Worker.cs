using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using OrderProcessing.Core.Configuration;
using OrderProcessing.Core.Data;
using OrderProcessing.Core.Models;
using System.Text.Json;

namespace OrderProcessing.OrderProcessor;

/// <summary>
/// Consumes orders from the "raw-orders" topic, validates them,
/// calculates totals, and publishes to "processed-orders".
/// This is the main processing engine of the pipeline.
/// </summary>
public class OrderProcessorWorker : BackgroundService
{
    private readonly ILogger<OrderProcessorWorker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly string _bootstrapServers;

    public OrderProcessorWorker(
        ILogger<OrderProcessorWorker> logger,
        IConfiguration config,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _bootstrapServers = config.GetValue<string>("Kafka:BootstrapServers") ?? "localhost:9092";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OrderProcessorWorker started, waiting for messages...");

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = _bootstrapServers,
            GroupId = "order-processor-group",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
            AllowAutoCreateTopics = true
        };

        var producerConfig = new ProducerConfig
        {
            BootstrapServers = _bootstrapServers,
            Acks = Acks.All,
            EnableIdempotence = true,
            MessageSendMaxRetries = 3
        };

        using var consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
        using var producer = new ProducerBuilder<string, string>(producerConfig).Build();

        consumer.Subscribe(KafkaTopics.RawOrders);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var result = consumer.Consume(stoppingToken);
                    _logger.LogInformation("Received order from Kafka at offset {Offset}", result.Offset);

                    var order = JsonSerializer.Deserialize<Order>(result.Message.Value,
                        new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

                    if (order is null)
                    {
                        _logger.LogWarning("Failed to deserialize order message, skipping");
                        consumer.Commit(result);
                        continue;
                    }

                    await ProcessOrderAsync(order, stoppingToken);

                    // Publish processed order to next topic
                    var processedJson = JsonSerializer.Serialize(order,
                        new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

                    await producer.ProduceAsync(KafkaTopics.ProcessedOrders,
                        new Message<string, string>
                        {
                            Key = order.Id.ToString(),
                            Value = processedJson
                        }, stoppingToken);

                    consumer.Commit(result);

                    _logger.LogInformation(
                        "Order {OrderId} processed successfully → {Status}", order.Id, order.Status);
                }
                catch (ConsumeException ex) when (!ex.Error.IsFatal)
                {
                    _logger.LogWarning("Kafka consume error: {Reason}", ex.Error.Reason);
                    await Task.Delay(1000, stoppingToken);
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning("Malformed message skipping: {Message}", ex.Message);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("OrderProcessorWorker shutting down gracefully");
        }
        finally
        {
            consumer.Close();
        }
    }

    /// <summary>
    /// Validates the order and calculates the total price.
    /// Updates the order status based on validation results.
    /// </summary>
    private async Task ProcessOrderAsync(Order order, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();

        try
        {
            var dbOrder = await db.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == order.Id, ct);

            if (dbOrder is null)
            {
                _logger.LogWarning("Order {OrderId} not found in database, inserting", order.Id);
                order.Status = OrderStatus.Processing;
                await Task.Delay(100, ct); // Simulate async processing
                db.Orders.Add(order);
                await db.SaveChangesAsync(ct);
                return;
            }

            // Mark as processing
            dbOrder.Status = OrderStatus.Processing;
            dbOrder.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            // Validate order
            var (isValid, errorMessage) = ValidateOrder(dbOrder);

            if (!isValid)
            {
                dbOrder.Status = OrderStatus.Failed;
                dbOrder.ErrorMessage = errorMessage;
                await db.SaveChangesAsync(ct);

                _logger.LogWarning("Order {OrderId} validation failed: {Error}", order.Id, errorMessage);
                return;
            }

            // Calculate total price from items
            dbOrder.TotalPrice = dbOrder.Items.Sum(i => i.UnitPrice * i.Quantity);
            dbOrder.Status = OrderStatus.Processed;
            dbOrder.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            // Sync the DTO order status for Kafka publishing
            order.Status = OrderStatus.Processed;
            order.TotalPrice = dbOrder.TotalPrice;

            _logger.LogInformation(
                "Order {OrderId} validated, total: {Total:C}", order.Id, order.TotalPrice);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing order {OrderId}", order.Id);
            throw;
        }
    }

    /// <summary>
    /// Validates order business rules.
    /// Returns (isValid, errorMessage).
    /// </summary>
    private static (bool IsValid, string? ErrorMessage) ValidateOrder(Order order)
    {
        if (order.Items.Count == 0)
            return (false, "Order must contain at least one item.");

        if (order.Items.Any(i => i.Quantity <= 0))
            return (false, "All item quantities must be greater than zero.");

        if (order.Items.Any(i => i.UnitPrice <= 0))
            return (false, "All item unit prices must be greater than zero.");

        if (string.IsNullOrWhiteSpace(order.CustomerEmail) || !order.CustomerEmail.Contains('@'))
            return (false, "A valid customer email is required.");

        return (true, null);
    }
}
