using RestaurantPos.Domain.Common;

namespace RestaurantPos.Domain.Orders;

public class Order
{
    public const int MaxQuantityPerLine = 99;

    private readonly List<OrderLine> _lines = new();

    private Order() { } // needed by EF Core

    public Guid Id { get; private set; }
    public int TableNumber { get; private set; }
    public OrderStatus Status { get; private set; }

    // Tax in basis points: 500 means 5.00%.
    public int TaxRateBasisPoints { get; private set; }
    public int DiscountPercent { get; private set; }

    public IReadOnlyList<OrderLine> Lines => _lines;

    public DateTime CreatedAt { get; private set; }
    public DateTime? SentAt { get; private set; }
    public DateTime? PaidAt { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public string? CancelReason { get; private set; }

    public PaymentMethod? PaidWith { get; private set; }
    public long? TenderedCents { get; private set; }
    public long? ChangeCents { get; private set; }

    // ---- Money. All amounts are in cents, so there are no floating-point errors. ----

    public long SubtotalCents => _lines.Sum(l => l.LineTotalCents);

    // Rounded half up.
    public long DiscountCents => (SubtotalCents * DiscountPercent + 50) / 100;

    public long TaxCents => ((SubtotalCents - DiscountCents) * TaxRateBasisPoints + 5000) / 10000;

    public long TotalCents => SubtotalCents - DiscountCents + TaxCents;

    // ---- Behaviour ----

    public static Order Open(int tableNumber, int taxRateBasisPoints, DateTime nowUtc)
    {
        if (tableNumber < 1) throw new DomainException("Table number must be 1 or more.");
        if (taxRateBasisPoints is < 0 or > 10000) throw new DomainException("Tax rate must be between 0% and 100%.");

        return new Order
        {
            Id = Guid.NewGuid(),
            TableNumber = tableNumber,
            Status = OrderStatus.Open,
            TaxRateBasisPoints = taxRateBasisPoints,
            CreatedAt = nowUtc
        };
    }

    public void AddLine(Guid menuItemId, string itemName, long unitPriceCents, int quantity)
    {
        EnsureOpen("add items");

        if (quantity < 1 || quantity > MaxQuantityPerLine)
            throw new DomainException($"Quantity must be between 1 and {MaxQuantityPerLine}.");

        var existing = _lines.FirstOrDefault(l => l.MenuItemId == menuItemId);
        if (existing is not null)
        {
            if (existing.Quantity + quantity > MaxQuantityPerLine)
                throw new DomainException($"Quantity per item cannot be more than {MaxQuantityPerLine}.");

            existing.IncreaseQuantity(quantity);
        }
        else
        {
            _lines.Add(OrderLine.Create(Id, menuItemId, itemName, unitPriceCents, quantity));
        }
    }

    public void RemoveLine(Guid menuItemId)
    {
        EnsureOpen("remove items");

        var line = _lines.FirstOrDefault(l => l.MenuItemId == menuItemId)
            ?? throw new DomainException("That item is not on the order.");

        _lines.Remove(line);
    }

    public void ApplyDiscount(int percent)
    {
        EnsureOpen("change the discount");

        if (percent is < 0 or > 100) throw new DomainException("Discount must be between 0% and 100%.");
        DiscountPercent = percent;
    }

    public void SendToKitchen(DateTime nowUtc)
    {
        EnsureOpen("send to the kitchen");

        if (_lines.Count == 0) throw new DomainException("An empty order cannot be sent to the kitchen.");

        Status = OrderStatus.SentToKitchen;
        SentAt = nowUtc;
    }

    public void Pay(PaymentMethod method, long tenderedCents, DateTime nowUtc)
    {
        if (Status == OrderStatus.Paid) throw new DomainException("The order is already paid.");
        if (Status == OrderStatus.Cancelled) throw new DomainException("A cancelled order cannot be paid.");
        if (_lines.Count == 0) throw new DomainException("An empty order cannot be paid.");

        var total = TotalCents;

        if (method == PaymentMethod.Card && tenderedCents != total)
            throw new DomainException("A card payment must be for the exact total.");

        if (tenderedCents < total)
            throw new DomainException("The amount given is less than the total.");

        Status = OrderStatus.Paid;
        PaidWith = method;
        TenderedCents = tenderedCents;
        ChangeCents = tenderedCents - total;
        PaidAt = nowUtc;
    }

    public void Cancel(string reason, DateTime nowUtc)
    {
        if (Status == OrderStatus.Paid) throw new DomainException("A paid order cannot be cancelled.");
        if (Status == OrderStatus.Cancelled) throw new DomainException("The order is already cancelled.");
        if (string.IsNullOrWhiteSpace(reason)) throw new DomainException("A reason is required to cancel an order.");

        Status = OrderStatus.Cancelled;
        CancelReason = reason.Trim();
        CancelledAt = nowUtc;
    }

    private void EnsureOpen(string action)
    {
        if (Status != OrderStatus.Open)
            throw new DomainException($"You cannot {action}: the order is {Status}.");
    }
}
