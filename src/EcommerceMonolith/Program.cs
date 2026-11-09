using Microsoft.EntityFrameworkCore;
using Monolith.Api;
using Monolith.Data;
using Monolith.Services;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Simple Monolith: ONE process, ONE deployable unit, ONE relational database.
// ---------------------------------------------------------------------------
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("Sqlite") ?? "Data Source=monolith.db"));

builder.Services.AddScoped<CatalogService>();
builder.Services.AddScoped<CartService>();
builder.Services.AddScoped<InventoryService>();
builder.Services.AddScoped<PaymentsService>();
builder.Services.AddScoped<OrdersService>();

// JSON: enums as strings (Paid/Cancelled/…). Navigation cycles are broken
// with [JsonIgnore] on the back-references of the entities.
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});

// Liveness/readiness probe used by the Docker HEALTHCHECK and by compose.
builder.Services.AddHealthChecks();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
    await SeedData.RunAsync(db);
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapMonolithApi();
app.MapHealthChecks("/health");

app.Run();
