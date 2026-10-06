using RestaurantPos.Domain.Orders;

namespace RestaurantPos.Application.Orders;

public record OpenOrderRequest(int TableNumber);

public record AddLineRequest(Guid MenuItemId, int Quantity);

public record DiscountRequest(int Percent);

public record PayRequest(PaymentMethod Method, long TenderedCents);

public record CancelRequest(string Reason);

public record OrderLineDto(Guid MenuItemId, string ItemName, long UnitPriceCents, int Quantity, long LineTotalCents);

public record OrderDto(
    Guid Id,
    int TableNumber,
    string Status,
    IReadOnlyList<OrderLineDto> Lines,
    long SubtotalCents,
    int DiscountPercent,
    long DiscountCents,
    int TaxRateBasisPoints,
    long TaxCents,
    long TotalCents,
    string? PaidWith,
    long? TenderedCents,
    long? ChangeCents,
    DateTime CreatedAt,
    DateTime? SentAt,
    DateTime? PaidAt)
{
    public static OrderDto From(Order o) => new(
        o.Id,
        o.TableNumber,
        o.Status.ToString(),
        o.Lines.Select(l => new OrderLineDto(l.MenuItemId, l.ItemName, l.UnitPriceCents, l.Quantity, l.LineTotalCents)).ToList(),
        o.SubtotalCents,
        o.DiscountPercent,
        o.DiscountCents,
        o.TaxRateBasisPoints,
        o.TaxCents,
        o.TotalCents,
        o.PaidWith?.ToString(),
        o.TenderedCents,
        o.ChangeCents,
        o.CreatedAt,
        o.SentAt,
        o.PaidAt);
}
