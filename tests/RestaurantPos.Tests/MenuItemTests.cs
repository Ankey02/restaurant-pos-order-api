using RestaurantPos.Domain.Common;
using RestaurantPos.Domain.MenuItems;

namespace RestaurantPos.Tests;

public class MenuItemTests
{
    [Fact]
    public void Create_makes_an_available_item()
    {
        var item = MenuItem.Create("  Burger ", " Mains ", 1000, DateTime.UtcNow);

        Assert.Equal("Burger", item.Name);
        Assert.Equal("Mains", item.Category);
        Assert.Equal(1000L, item.PriceCents);
        Assert.True(item.IsAvailable);
    }

    [Theory]
    [InlineData("", "Mains", 100)]
    [InlineData("Burger", "", 100)]
    [InlineData("Burger", "Mains", 0)]
    [InlineData("Burger", "Mains", -5)]
    public void Create_rejects_invalid_input(string name, string category, long price)
    {
        Assert.Throws<DomainException>(() => MenuItem.Create(name, category, price, DateTime.UtcNow));
    }

    [Fact]
    public void ChangePrice_updates_the_price()
    {
        var item = MenuItem.Create("Burger", "Mains", 1000, DateTime.UtcNow);

        item.ChangePrice(1200);

        Assert.Equal(1200L, item.PriceCents);
    }

    [Fact]
    public void ChangePrice_rejects_zero()
    {
        var item = MenuItem.Create("Burger", "Mains", 1000, DateTime.UtcNow);

        Assert.Throws<DomainException>(() => item.ChangePrice(0));
    }
}
