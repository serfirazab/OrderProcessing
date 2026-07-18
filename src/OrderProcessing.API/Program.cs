using Microsoft.EntityFrameworkCore;
using OrderProcessing.API.Services;
using OrderProcessing.Core.Data;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

var kafkaBootstrapServers = builder.Configuration.GetValue<string>("Kafka:BootstrapServers") ?? "localhost:9092";

// ── Services ──────────────────────────────────────────────────────

// Controllers + OpenAPI
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Blazor Server with interactive server-side rendering
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// EF Core - SQLite (portable, file-based database)
builder.Services.AddDbContext<OrderDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Kafka - topic initialization on startup
builder.Services.AddSingleton(new KafkaTopicInitializer(kafkaBootstrapServers));
builder.Services.AddHostedService(sp => sp.GetRequiredService<KafkaTopicInitializer>());

// Kafka - order publisher (singleton for connection pooling)
builder.Services.AddSingleton<OrderPublisherService>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<OrderPublisherService>>();
    return new OrderPublisherService(kafkaBootstrapServers, logger);
});

// ── Middleware Pipeline ───────────────────────────────────────────

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Scalar API documentation UI
app.MapScalarApiReference(options =>
{
    options.WithTitle("OrderProcessing API")
           .WithTheme(ScalarTheme.Mars);
});

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapControllers();
app.MapRazorComponents<OrderProcessing.API.Components.App>()
   .AddInteractiveServerRenderMode();

// Auto-apply EF migrations on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
    db.Database.EnsureCreated();
}

app.Run();
