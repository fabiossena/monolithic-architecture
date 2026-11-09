using Microsoft.EntityFrameworkCore;
using Monolith.Data;
using Monolith.Domain;

namespace Monolith.Services;

// Inventory Service — the only writer of stock quantities.
public class InventoryService(AppDbContext db)
{
    public async Task<List<InventoryItem>> GetAllAsync() =>
        await db.Inventory.AsNoTracking()
            .Include(i => i.Product)
            .OrderBy(i => i.Product!.Name)
            .ToListAsync();

    public Task<int> GetQuantityAsync(int productId) =>
        db.Inventory.AsNoTracking()
            .Where(i => i.ProductId == productId)
            .Select(i => i.Quantity)
            .FirstOrDefaultAsync();

    public async Task<Dictionary<int, int>> GetQuantitiesAsync(IEnumerable<int> productIds)
    {
        var ids = productIds.Distinct().ToList();
        return await db.Inventory.AsNoTracking()
            .Where(i => ids.Contains(i.ProductId))
            .ToDictionaryAsync(i => i.ProductId, i => i.Quantity);
    }

    /// <summary>Reserve (decrement) stock. Returns false when there is not enough.</summary>
    public async Task<bool> ReserveAsync(int productId, int quantity)
    {
        var item = await db.Inventory.FirstOrDefaultAsync(i => i.ProductId == productId);
        if (item is null || item.Quantity < quantity) return false;

        item.Quantity -= quantity;
        item.UpdatedAt = DateTime.UtcNow;
        return true;
    }

    public async Task<InventoryItem?> AdjustAsync(int productId, int quantity, int? reorderLevel)
    {
        var item = await db.Inventory.FirstOrDefaultAsync(i => i.ProductId == productId);
        if (item is null) return null;

        item.Quantity = Math.Max(0, quantity);
        if (reorderLevel is not null) item.ReorderLevel = Math.Max(0, reorderLevel.Value);
        item.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return item;
    }

    public async Task<InventoryItem?> AddStockAsync(int productId, int delta)
    {
        var item = await db.Inventory.FirstOrDefaultAsync(i => i.ProductId == productId);
        if (item is null) return null;

        item.Quantity = Math.Max(0, item.Quantity + delta);
        item.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return item;
    }
}
