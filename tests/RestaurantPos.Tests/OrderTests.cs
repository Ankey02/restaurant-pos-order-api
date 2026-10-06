using RestaurantPos.Domain.Common;
using RestaurantPos.Domain.Orders;

namespace RestaurantPos.Tests;

public class OrderTests
{
    private static readonly Guid Burger = Guid.NewGuid();
    private static readonly Guid Fries = Guid.NewGuid();
    private static readonly DateTime Now = DateTime.UtcNow;

    // Table 4, 5% tax: 2 burgers at 10.00 and 1 fries at 5.00 = subtotal 25.00.
    private static Order NewOrder()
    {
        var order = Order.Open(4, 500, Now);
        order.AddLine(Burger, "Burger", 1000, 2);
        order.AddLine(Fries, "Fries", 500, 1);
        return order;
    }

    [Fact]
    public void Open_creates_an_empty_open_order()
    {
        var order = Order.Open(4, 500, Now);

        Assert.Equal(OrderStatus.Open, order.Status);
        Assert.Equal(4, order.TableNumber);
        Assert.Empty(order.Lines);
        Assert.Equal(0L, order.TotalCents);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Open_rejects_a_bad_table_number(int table)
    {
        Assert.Throws<DomainException>(() => Order.Open(table, 500, Now));
    }

    [Fact]
    public void AddLine_merges_the_same_item_into_one_line()
    {
        var order = Order.Open(1, 500, Now);

        order.AddLine(Burger, "Burger", 1000, 2);
        order.AddLine(Burger, "Burger", 1000, 3);

        var line = Assert.Single(order.Lines);
        Assert.Equal(5, line.Quantity);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(100)]
    public void AddLine_rejects_a_bad_quantity(int quantity)
    {
        var order = Order.Open(1, 500, Now);

        Assert.Throws<DomainException>(() => order.AddLine(Burger, "Burger", 1000, quantity));
    }

    [Fact]
    public void AddLine_rejects_a_merged_quantity_over_99()
    {
        var order = Order.Open(1, 500, Now);
        order.AddLine(Burger, "Burger", 1000, 90);

        Assert.Throws<DomainException>(() => order.AddLine(Burger, "Burger", 1000, 10));
    }

    [Fact]
    public void Totals_add_tax_to_the_subtotal()
    {
        var order = NewOrder();

        Assert.Equal(2500L, order.SubtotalCents);
        Assert.Equal(125L, order.TaxCents);
        Assert.Equal(2625L, order.TotalCents);
    }

    [Fact]
    public void Totals_apply_the_discount_before_tax_and_round_half_up()
    {
        var order = NewOrder();

        order.ApplyDiscount(10);

        Assert.Equal(250L, order.DiscountCents);  // 10% of 25.00
        Assert.Equal(113L, order.TaxCents);       // 5% of 22.50 = 1.125, rounded to 1.13
        Assert.Equal(2363L, order.TotalCents);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void ApplyDiscount_rejects_values_outside_0_to_100(int percent)
    {
        var order = NewOrder();

        Assert.Throws<DomainException>(() => order.ApplyDiscount(percent));
    }

    [Fact]
    public void RemoveLine_removes_the_item()
    {
        var order = NewOrder();

        order.RemoveLine(Fries);

        var line = Assert.Single(order.Lines);
        Assert.Equal(Burger, line.MenuItemId);
    }

    [Fact]
    public void RemoveLine_rejects_an_item_that_is_not_on_the_order()
    {
        var order = Order.Open(1, 500, Now);

        Assert.Throws<DomainException>(() => order.RemoveLine(Burger));
    }

    [Fact]
    public void SendToKitchen_needs_at_least_one_item()
    {
        var order = Order.Open(1, 500, Now);

        Assert.Throws<DomainException>(() => order.SendToKitchen(Now));
    }

    [Fact]
    public void SendToKitchen_locks_the_order_for_editing()
    {
        var order = NewOrder();

        order.SendToKitchen(Now);

        Assert.Equal(OrderStatus.SentToKitchen, order.Status);
        Assert.Throws<DomainException>(() => order.AddLine(Fries, "Fries", 500, 1));
        Assert.Throws<DomainException>(() => order.RemoveLine(Burger));
        Assert.Throws<DomainException>(() => order.ApplyDiscount(5));
    }

    [Fact]
    public void Pay_with_cash_gives_change()
    {
        var order = NewOrder();

        order.Pay(PaymentMethod.Cash, 3000, Now);

        Assert.Equal(OrderStatus.Paid, order.Status);
        Assert.Equal(375L, order.ChangeCents!.Value);
    }

    [Fact]
    public void Pay_rejects_too_little_cash()
    {
        var order = NewOrder();

        Assert.Throws<DomainException>(() => order.Pay(PaymentMethod.Cash, 2000, Now));
        Assert.Equal(OrderStatus.Open, order.Status);
    }

    [Fact]
    public void Pay_by_card_must_match_the_total_exactly()
    {
        var order = NewOrder();

        Assert.Throws<DomainException>(() => order.Pay(PaymentMethod.Card, order.TotalCents + 1, Now));

        order.Pay(PaymentMethod.Card, order.TotalCents, Now);
        Assert.Equal(OrderStatus.Paid, order.Status);
        Assert.Equal(0L, order.ChangeCents!.Value);
    }

    [Fact]
    public void Pay_rejects_an_empty_order()
    {
        var order = Order.Open(1, 500, Now);

        Assert.Throws<DomainException>(() => order.Pay(PaymentMethod.Cash, 1000, Now));
    }

    [Fact]
    public void Pay_rejects_an_order_that_is_already_paid()
    {
        var order = NewOrder();
        order.Pay(PaymentMethod.Cash, 3000, Now);

        Assert.Throws<DomainException>(() => order.Pay(PaymentMethod.Cash, 3000, Now));
    }

    [Fact]
    public void Pay_rejects_a_cancelled_order()
    {
        var order = NewOrder();
        order.Cancel("Guest left", Now);

        Assert.Throws<DomainException>(() => order.Pay(PaymentMethod.Cash, 3000, Now));
    }

    [Fact]
    public void Cancel_sets_the_status_and_reason()
    {
        var order = NewOrder();
        order.SendToKitchen(Now);

        order.Cancel("  Kitchen ran out of burgers ", Now);

        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.Equal("Kitchen ran out of burgers", order.CancelReason);
    }

    [Fact]
    public void Cancel_needs_a_reason()
    {
        var order = NewOrder();

        Assert.Throws<DomainException>(() => order.Cancel("  ", Now));
    }

    [Fact]
    public void Cancel_rejects_a_paid_order()
    {
        var order = NewOrder();
        order.Pay(PaymentMethod.Cash, 3000, Now);

        Assert.Throws<DomainException>(() => order.Cancel("Changed my mind", Now));
    }
}
