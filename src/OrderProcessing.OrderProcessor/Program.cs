using Microsoft.EntityFrameworkCore;
using OrderProcessing.Core.Data;
using OrderProcessing.OrderProcessor;

var builder = Host.CreateApplicationBuilder(args);

// EF Core - SQLite
builder.Services.AddDbContext<OrderDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

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
