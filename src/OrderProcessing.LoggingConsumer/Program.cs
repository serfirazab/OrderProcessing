using OrderProcessing.LoggingConsumer;

var builder = Host.CreateApplicationBuilder(args);

// Kafka consumer worker
builder.Services.AddHostedService<LoggingConsumerWorker>();

var host = builder.Build();
host.Run();
