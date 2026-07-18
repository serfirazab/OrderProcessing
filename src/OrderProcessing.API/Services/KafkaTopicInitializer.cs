using Confluent.Kafka;
using Confluent.Kafka.Admin;
using OrderProcessing.Core.Configuration;

namespace OrderProcessing.API.Services;

/// <summary>
/// Ensures required Kafka topics exist on application startup.
/// Idempotent — topics are created only if they don't already exist.
/// </summary>
public class KafkaTopicInitializer : IHostedService
{
    private readonly string _bootstrapServers;

    public KafkaTopicInitializer(string bootstrapServers)
    {
        _bootstrapServers = bootstrapServers;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var topics = new[]
        {
            KafkaTopics.RawOrders,
            KafkaTopics.ProcessedOrders,
            KafkaTopics.OrderStatus
        };

        var config = new AdminClientConfig { BootstrapServers = _bootstrapServers };

        using var adminClient = new AdminClientBuilder(config).Build();

        try
        {
            var existingTopics = adminClient.GetMetadata(TimeSpan.FromSeconds(10))
                .Topics.Select(t => t.Topic).ToHashSet();

            foreach (var topic in topics)
            {
                if (!existingTopics.Contains(topic))
                {
                    await adminClient.CreateTopicsAsync([
                        new TopicSpecification
                        {
                            Name = topic,
                            NumPartitions = 1,
                            ReplicationFactor = 1
                        }
                    ]);

                    Console.WriteLine($"Created Kafka topic: {topic}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Could not initialize Kafka topics: {ex.Message}");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
