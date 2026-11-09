using Microsoft.EntityFrameworkCore;
using Monolith.Data;
using Monolith.Domain;

namespace Monolith.Services;

public record CheckoutCommand(
    string CartId,
    string CustomerName,
    string Email,
    string Address,
    string City,
    string Zip,
    string PaymentMethod,
    bool SimulateFailure);

public record CheckoutResult(bool Ok, string? Error, Order? Order);

// Orders Service — orchestrates cart, payments and inventory inside ONE
// database transaction (the monolith's shared schema makes this trivial).
public class OrdersService(AppDbContext db, CartService cart, InventoryService inventory, PaymentsService payments)
{
    public async Task<CheckoutResult> CheckoutAsync(CheckoutCommand cmd)
    {
        if (string.IsNullOrWhiteSpace(cmd.CartId))
            return new CheckoutResult(false, "Empty cart.", null);
        if (string.IsNullOrWhiteSpace(cmd.CustomerName) || string.IsNullOrWhiteSpace(cmd.Email))
            return new CheckoutResult(false, "Name and e-mail are required.", null);

        var cartView = await cart.GetAsync(cmd.CartId);
        if (cartView.Items.Count == 0)
            return new CheckoutResult(false, "Empty cart.", null);

        await using var tx = await db.Database.BeginTransactionAsync();

        // 1) Revalidate stock inside the transaction (single source of truth).
        foreach (var line in cartView.Items)
        {
            var available = await inventory.GetQuantityAsync(line.ProductId);
            if (available < line.Quantity)
                return new CheckoutResult(false,
                    $"Not enough stock for \"{line.Name}\" (available: {available}).", null);
        }

        // 2) Charge the payment gateway (simulated).
        var charge = await payments.ChargeAsync(cartView.Subtotal, cmd.PaymentMethod, cmd.SimulateFailure);

        // 3) Create the order (+ payment record).
        var order = new Order
        {
            CustomerName = cmd.CustomerName.Trim(),
            Email = cmd.Email.Trim(),
            Address = cmd.Address.Trim(),
            City = cmd.City.Trim(),
            Zip = cmd.Zip.Trim(),
            Total = cartView.Subtotal,
            Status = charge.Approved ? OrderStatus.Paid : OrderStatus.Cancelled,
            Items = cartView.Items.Select(l => new OrderItem
            {
                ProductId = l.ProductId,
                Name = l.Name,
                Sku = l.Sku,
                Emoji = l.Emoji,
                UnitPrice = l.Price,
                Quantity = l.Quantity
            }).ToList(),
            Payment = new Payment
            {
                Amount = cartView.Subtotal,
                Method = cmd.PaymentMethod,
                Status = charge.Approved ? PaymentStatus.Approved : PaymentStatus.Declined,
                TransactionId = charge.TransactionId,
                Reason = charge.Reason
            }
        };

        db.Orders.Add(order);
        await db.SaveChangesAsync();
        order.Code = $"ORD-{order.Id:D5}";

        // 4) Approved -> reserve stock; declined -> nothing leaves inventory.
        if (charge.Approved)
        {
            foreach (var line in cartView.Items)
            {
                var reserved = await inventory.ReserveAsync(line.ProductId, line.Quantity);
                if (!reserved)
                {
                    await tx.RollbackAsync();
                    return new CheckoutResult(false, $"Failed to reserve stock for \"{line.Name}\".", null);
                }
            }
            await cart.ClearAsync(cmd.CartId);
        }

        await db.SaveChangesAsync();
        await tx.CommitAsync();

        var saved = await GetAsync(order.Id);
        return new CheckoutResult(true, charge.Reason, saved);
    }

    public async Task<List<Order>> ListAsync() =>
        await db.Orders.AsNoTracking()
            .Include(o => o.Items)
            .Include(o => o.Payment)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

    public Task<Order?> GetAsync(int id) =>
        db.Orders.AsNoTracking()
            .Include(o => o.Items)
            .Include(o => o.Payment)
            .FirstOrDefaultAsync(o => o.Id == id);
}
