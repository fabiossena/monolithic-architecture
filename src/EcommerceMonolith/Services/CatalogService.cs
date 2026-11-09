using Microsoft.EntityFrameworkCore;
using Monolith.Data;
using Monolith.Domain;

namespace Monolith.Services;

// Catalog Service — read model over the shared schema.
public class CatalogService(AppDbContext db)
{
    public async Task<List<Product>> SearchAsync(string? q, string? category)
    {
        var query = db.Products.AsNoTracking().Where(p => p.Active);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            query = query.Where(p =>
                p.Name.ToLower().Contains(term) ||
                p.Description.ToLower().Contains(term) ||
                p.Sku.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(p => p.Category == category);

        return await query.OrderBy(p => p.Name).ToListAsync();
    }

    public Task<Product?> GetAsync(int id) =>
        db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);

    public Task<List<string>> CategoriesAsync() =>
        db.Products.AsNoTracking()
            .Where(p => p.Active)
            .Select(p => p.Category)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();
}
