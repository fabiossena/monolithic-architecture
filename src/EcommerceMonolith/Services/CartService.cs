using Microsoft.EntityFrameworkCore;
using Monolith.Data;
using Monolith.Domain;

namespace Monolith.Services;

public record CartLine(int ProductId, string Name, string Emoji, string Sku,
    decimal Price, int Quantity, int Stock, decimal Subtotal);

public record CartView(string Id, List<CartLine> Items, decimal Subtotal, int TotalItems);

// Cart Service — session-like state for a single visitor.
public class CartService(AppDbContext db, InventoryService inventory)
{
    public async Task<CartView> GetAsync(string cartId)
    {
        var cart = await db.Carts.AsNoTracking()
            .Include(c => c.Items)
            .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(c => c.Id == cartId);

        return await ToViewAsync(cart);
    }

    public async Task<CartView> AddAsync(string cartId, int productId, int quantity)
    {
        if (quantity < 1) quantity = 1;

        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == productId && p.Active)
                      ?? throw new KeyNotFoundException("Product not found.");

        var stock = await inventory.GetQuantityAsync(productId);
        var cart = await db.Carts.Include(c => c.Items)
                     .FirstOrDefaultAsync(c => c.Id == cartId)
                     ?? db.Carts.Add(new Cart { Id = cartId }).Entity;

        var line = cart.Items.FirstOrDefault(i => i.ProductId == productId);
        var desired = (line?.Quantity ?? 0) + quantity;

        if (desired > stock)
            throw new InvalidOperationException(
                stock == 0 ? $"\"{product.Name}\" is out of stock."
                           : $"Only {stock} unit(s) left of \"{product.Name}\".");

        if (line is null) cart.Items.Add(new CartItem { ProductId = productId, Quantity = desired });
        else line.Quantity = desired;

        await db.SaveChangesAsync();
        return await GetAsync(cartId);
    }

    public async Task<CartView> SetQuantityAsync(string cartId, int productId, int quantity)
    {
        var cart = await db.Carts.Include(c => c.Items)
                     .FirstOrDefaultAsync(c => c.Id == cartId);
        if (cart is null) return await ToViewAsync(null);

        var line = cart.Items.FirstOrDefault(i => i.ProductId == productId);
        if (line is null) return await ToViewAsync(cart);

        if (quantity <= 0)
        {
            cart.Items.Remove(line);
            db.CartItems.Remove(line);
        }
        else
        {
            var stock = await inventory.GetQuantityAsync(productId);
            if (quantity > stock)
                throw new InvalidOperationException($"Only {stock} unit(s) left in stock.");
            line.Quantity = quantity;
        }

        await db.SaveChangesAsync();
        return await GetAsync(cartId);
    }

    public async Task RemoveAsync(string cartId, int productId)
    {
        var cart = await db.Carts.Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.Id == cartId);
        if (cart is null) return;

        var line = cart.Items.FirstOrDefault(i => i.ProductId == productId);
        if (line is null) return;

        cart.Items.Remove(line);
        db.CartItems.Remove(line);
        await db.SaveChangesAsync();
    }

    public async Task ClearAsync(string cartId)
    {
        var cart = await db.Carts.Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.Id == cartId);
        if (cart is null) return;

        db.CartItems.RemoveRange(cart.Items);
        cart.Items.Clear();
        await db.SaveChangesAsync();
    }

    private async Task<CartView> ToViewAsync(Cart? cart)
    {
        if (cart is null) return new CartView("", [], 0, 0);

        var lines = cart.Items.Where(i => i.Product is not null).ToList();
        var stocks = await inventory.GetQuantitiesAsync(lines.Select(i => i.ProductId));

        var items = lines
            .Select(i =>
            {
                stocks.TryGetValue(i.ProductId, out var stock);
                return new CartLine(i.ProductId, i.Product!.Name, i.Product.Emoji, i.Product.Sku,
                    i.Product.Price, i.Quantity, stock, i.Product.Price * i.Quantity);
            })
            .ToList();

        return new CartView(cart.Id, items, items.Sum(i => i.Subtotal), items.Sum(i => i.Quantity));
    }
}
