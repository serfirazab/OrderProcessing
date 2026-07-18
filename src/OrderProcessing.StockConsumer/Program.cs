using OrderProcessing.StockConsumer;

var builder = Host.CreateApplicationBuilder(args);

// Kafka consumer worker
builder.Services.AddHostedService<StockConsumerWorker>();

var host = builder.Build();
host.Run();
