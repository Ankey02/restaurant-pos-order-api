using Microsoft.EntityFrameworkCore;
using RestaurantPos.Application.Common;
using RestaurantPos.Domain.Common;
using RestaurantPos.Domain.Orders;

namespace RestaurantPos.Application.Orders;

public class OrderCommands(IAppDbContext db, TimeProvider clock, RestaurantSettings settings)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public async Task<OrderDto> OpenAsync(OpenOrderRequest request, CancellationToken ct)
    {
        var order = Order.Open(request.TableNumber, settings.TaxRateBasisPoints, Now);

        var tableBusy = await db.Orders.AnyAsync(
            o => o.TableNumber == request.TableNumber &&
                 (o.Status == OrderStatus.Open || o.Status == OrderStatus.SentToKitchen), ct);
        if (tableBusy) throw TableBusy(request.TableNumber);

        db.Orders.Add(order);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Two waiters opened the same table at the same moment: the database's unique index stopped the second one.
            throw TableBusy(request.TableNumber);
        }

        return OrderDto.From(order);
    }

    public async Task<OrderDto> AddLineAsync(Guid orderId, AddLineRequest request, CancellationToken ct)
    {
        var item = await db.MenuItems.AsNoTracking().FirstOrDefaultAsync(m => m.Id == request.MenuItemId, ct)
                   ?? throw new NotFoundException("Menu item not found.");

        if (!item.IsAvailable) throw new DomainException($"{item.Name} is not available right now.");

        // The order keeps a copy of the name and price from this moment.
        return await MutateAsync(orderId, o => o.AddLine(item.Id, item.Name, item.PriceCents, request.Quantity), ct);
    }

    public Task<OrderDto> RemoveLineAsync(Guid orderId, Guid menuItemId, CancellationToken ct) =>
        MutateAsync(orderId, o => o.RemoveLine(menuItemId), ct);

    public Task<OrderDto> ApplyDiscountAsync(Guid orderId, DiscountRequest request, CancellationToken ct) =>
        MutateAsync(orderId, o => o.ApplyDiscount(request.Percent), ct);

    public Task<OrderDto> SendToKitchenAsync(Guid orderId, CancellationToken ct) =>
        MutateAsync(orderId, o => o.SendToKitchen(Now), ct);

    public Task<OrderDto> PayAsync(Guid orderId, PayRequest request, CancellationToken ct) =>
        MutateAsync(orderId, o => o.Pay(request.Method, request.TenderedCents, Now), ct);

    public Task<OrderDto> CancelAsync(Guid orderId, CancelRequest request, CancellationToken ct) =>
        MutateAsync(orderId, o => o.Cancel(request.Reason, Now), ct);

    // Load the order, apply one change through the domain rules, save.
    private async Task<OrderDto> MutateAsync(Guid orderId, Action<Order> change, CancellationToken ct)
    {
        var order = await db.Orders.Include(o => o.Lines).FirstOrDefaultAsync(o => o.Id == orderId, ct)
                    ?? throw new NotFoundException("Order not found.");

        change(order);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("The order was changed by someone else. Reload it and try again.");
        }

        return OrderDto.From(order);
    }

    private static ConflictException TableBusy(int table) =>
        new($"Table {table} already has an active order.");
}
