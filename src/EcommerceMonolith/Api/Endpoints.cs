using Monolith.Services;

namespace Monolith.Api;

// API/UI Controller layer: the single entry point that the UI (and any other
// client) talks to. All services are reached only through these endpoints.
public static class Endpoints
{
    public static void MapMonolithApi(this WebApplication app)
    {
        var api = app.MapGroup("/api");

        // ---------------- Catalog ----------------
        var catalog = api.MapGroup("/catalog");

        catalog.MapGet("/products", async (CatalogService svc, string? q, string? category) =>
            Results.Ok(await svc.SearchAsync(q, category)));

        catalog.MapGet("/categories", async (CatalogService svc) =>
            Results.Ok(await svc.CategoriesAsync()));

        catalog.MapGet("/products/{id:int}", async (CatalogService svc, int id) =>
            await svc.GetAsync(id) is { } p ? Results.Ok(p) : Results.NotFound());

        // ---------------- Inventory ----------------
        var inventory = api.MapGroup("/inventory");

        inventory.MapGet("", async (InventoryService svc) =>
            Results.Ok(await svc.GetAllAsync()));

        inventory.MapPut("/{productId:int}/quantity", async (InventoryService svc, int productId, AdjustRequest req) =>
            await svc.AdjustAsync(productId, req.Quantity, req.ReorderLevel) is { } item
                ? Results.Ok(item)
                : Results.NotFound());

        inventory.MapPost("/{productId:int}/restock", async (InventoryService svc, int productId, RestockRequest req) =>
            await svc.AddStockAsync(productId, req.Delta) is { } item
                ? Results.Ok(item)
                : Results.NotFound());

        // ---------------- Cart ----------------
        var cart = api.MapGroup("/cart/{cartId}");

        cart.MapGet("", async (CartService svc, string cartId) => Results.Ok(await svc.GetAsync(cartId)));

        cart.MapPost("/items", async (CartService svc, string cartId, AddCartItemRequest req) =>
        {
            try { return Results.Ok(await svc.AddAsync(cartId, req.ProductId, req.Quantity)); }
            catch (KeyNotFoundException ex) { return Results.NotFound(new { error = ex.Message }); }
            catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
        });

        cart.MapPut("/items/{productId:int}", async (CartService svc, string cartId, int productId, SetQuantityRequest req) =>
        {
            try { return Results.Ok(await svc.SetQuantityAsync(cartId, productId, req.Quantity)); }
            catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
        });

        cart.MapDelete("/items/{productId:int}", async (CartService svc, string cartId, int productId) =>
        {
            await svc.RemoveAsync(cartId, productId);
            return Results.Ok(await svc.GetAsync(cartId));
        });

        cart.MapDelete("", async (CartService svc, string cartId) =>
        {
            await svc.ClearAsync(cartId);
            return Results.Ok(await svc.GetAsync(cartId));
        });

        // ---------------- Orders / Payments ----------------
        var orders = api.MapGroup("/orders");

        orders.MapGet("", async (OrdersService svc) => Results.Ok(await svc.ListAsync()));

        orders.MapGet("/{id:int}", async (OrdersService svc, int id) =>
            await svc.GetAsync(id) is { } o ? Results.Ok(o) : Results.NotFound());

        orders.MapPost("/checkout", async (OrdersService svc, CheckoutCommand cmd) =>
        {
            var result = await svc.CheckoutAsync(cmd);
            return result.Ok
                ? Results.Ok(result.Order)
                : Results.BadRequest(new { error = result.Error });
        });

        // ---------------- Self-description (nice for the demo UI) ----------------
        api.MapGet("/meta", () => Results.Ok(new
        {
            name = "E-commerce Monolith",
            process = "single deployable unit",
            services = new[] { "Catalog", "Cart", "Orders", "Payments", "Inventory" },
            database = "SQLite (single relational DB, shared schema)"
        }));
    }
}

public record AdjustRequest(int Quantity, int? ReorderLevel);
public record RestockRequest(int Delta);
public record AddCartItemRequest(int ProductId, int Quantity);
public record SetQuantityRequest(int Quantity);
