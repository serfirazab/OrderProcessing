using Microsoft.EntityFrameworkCore;
using OrderProcessing.Core.Data;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

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
