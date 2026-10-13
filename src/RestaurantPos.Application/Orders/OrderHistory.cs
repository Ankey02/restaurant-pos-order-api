namespace RestaurantPos.Application.Orders;

// One status change recorded by the database trigger trg_orders_status_audit.
// FromStatus is null for the first entry (the order was opened).
public record OrderStatusChangeDto(string? FromStatus, string ToStatus, DateTime ChangedAt);

public interface IOrderHistoryQueries
{
    Task<IReadOnlyList<OrderStatusChangeDto>> GetAsync(Guid orderId, CancellationToken ct);
}
