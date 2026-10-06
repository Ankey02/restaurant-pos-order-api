using Microsoft.EntityFrameworkCore;
using RestaurantPos.Application.Common;
using RestaurantPos.Domain.Orders;

namespace RestaurantPos.Application.Orders;

public class OrderQueries(IAppDbContext db)
{
    public async Task<OrderDto> GetAsync(Guid id, CancellationToken ct)
    {
        var order = await db.Orders.AsNoTracking().Include(o => o.Lines)
                        .FirstOrDefaultAsync(o => o.Id == id, ct)
                    ?? throw new NotFoundException("Order not found.");

        return OrderDto.From(order);
    }

    // Orders that are still being served: open or in the kitchen.
    public async Task<List<OrderDto>> ListActiveAsync(CancellationToken ct)
    {
        var orders = await db.Orders.AsNoTracking().Include(o => o.Lines)
            .Where(o => o.Status == OrderStatus.Open || o.Status == OrderStatus.SentToKitchen)
            .OrderBy(o => o.TableNumber)
            .ToListAsync(ct);

        return orders.Select(OrderDto.From).ToList();
    }
}
