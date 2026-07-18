using Confluent.Kafka;
using OrderProcessing.Core.Configuration;
using OrderProcessing.Core.Models;
using System.Text.Json;

namespace OrderProcessing.EmailConsumer;

/// <summary>
/// Consumes processed orders and simulates sending confirmation emails.
/// Demonstrates async I/O simulation (Task.Delay) with structured logging.
/// </summary>
public class EmailConsumerWorker : BackgroundService
{
    private readonly ILogger<EmailConsumerWorker> _logger;
    private readonly string _bootstrapServers;

    public EmailConsumerWorker(ILogger<EmailConsumerWorker> logger, IConfiguration config)
    {
        _logger = logger;
        _bootstrapServers = config.GetValue<string>("Kafka:BootstrapServers") ?? "localhost:9092";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("EmailConsumerWorker started, waiting for processed orders...");

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = _bootstrapServers,
            GroupId = "email-consumer-group",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
            AllowAutoCreateTopics = true
        };

        var producerConfig = new ProducerConfig
        {
            BootstrapServers = _bootstrapServers,
            Acks = Acks.All,
            EnableIdempotence = true
        };

        using var consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
        using var producer = new ProducerBuilder<string, string>(producerConfig).Build();

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
                        "📧 Sending confirmation email to {Email} for order {OrderId}...",
                        order.CustomerEmail, order.Id);

                    // Simulate async email sending (I/O-bound operation)
                    await Task.Delay(500, stoppingToken);

                    _logger.LogInformation(
                        "✅ Email sent to {Email} for order {OrderId}",
                        order.CustomerEmail, order.Id);

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
            _logger.LogInformation("EmailConsumerWorker shutting down gracefully");
        }
        finally
        {
            consumer.Close();
        }
    }
}
