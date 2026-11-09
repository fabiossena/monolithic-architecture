using Microsoft.EntityFrameworkCore;
using Monolith.Domain;

namespace Monolith.Data;

// Single relational database, shared schema — the "Single Relational Database"
// box of the simple monolith design.
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<InventoryItem> Inventory => Set<InventoryItem>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Payment> Payments => Set<Payment>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Product>().HasIndex(p => p.Sku).IsUnique();

        b.Entity<InventoryItem>()
            .HasOne(i => i.Product)
            .WithOne()
            .HasForeignKey<InventoryItem>(i => i.ProductId);

        b.Entity<CartItem>()
            .HasOne(c => c.Cart)
            .WithMany(c => c.Items)
            .HasForeignKey(c => c.CartId);

        b.Entity<OrderItem>()
            .HasOne(o => o.Order)
            .WithMany(o => o.Items)
            .HasForeignKey(o => o.OrderId);

        b.Entity<Payment>()
            .HasOne(p => p.Order)
            .WithOne(o => o.Payment)
            .HasForeignKey<Payment>(p => p.OrderId);
    }
}
