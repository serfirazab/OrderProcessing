using OrderProcessing.EmailConsumer;

var builder = Host.CreateApplicationBuilder(args);

// Kafka consumer worker
builder.Services.AddHostedService<EmailConsumerWorker>();

var host = builder.Build();
host.Run();
