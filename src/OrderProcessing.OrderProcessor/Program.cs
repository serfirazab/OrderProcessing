using Microsoft.EntityFrameworkCore;
using OrderProcessing.Core.Data;
using OrderProcessing.OrderProcessor;

var builder = Host.CreateApplicationBuilder(args);

// EF Core - SQLite (shared database with API)
var dbPath = Path.GetFullPath(
    Path.Combine(builder.Environment.ContentRootPath, "..", "..", "data", "orders.db"));
Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
builder.Services.AddDbContext<OrderDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

// Kafka consumer worker
builder.Services.AddHostedService<OrderProcessorWorker>();

var host = builder.Build();

// Ensure database exists
using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
    db.Database.EnsureCreated();
}

host.Run();
