namespace RestaurantPos.Domain.Orders;

public enum OrderStatus
{
    Open = 0,
    SentToKitchen = 1,
    Paid = 2,
    Cancelled = 3
}

public enum PaymentMethod
{
    Cash = 0,
    Card = 1
}
