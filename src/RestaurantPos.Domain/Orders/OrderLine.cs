namespace RestaurantPos.Domain.Orders;

public class OrderLine
{
    private OrderLine() { } // needed by EF Core

    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid MenuItemId { get; private set; }

    // A copy of the name and price at the time of ordering. Later menu changes don't alter old bills.
    public string ItemName { get; private set; } = "";
    public long UnitPriceCents { get; private set; }
    public int Quantity { get; private set; }

    public long LineTotalCents => UnitPriceCents * Quantity;

    internal static OrderLine Create(Guid orderId, Guid menuItemId, string itemName, long unitPriceCents, int quantity) =>
        new()
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            MenuItemId = menuItemId,
            ItemName = itemName,
            UnitPriceCents = unitPriceCents,
            Quantity = quantity
        };

    internal void IncreaseQuantity(int by) => Quantity += by;
}
