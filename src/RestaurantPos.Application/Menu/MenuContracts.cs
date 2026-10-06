using RestaurantPos.Domain.MenuItems;

namespace RestaurantPos.Application.Menu;

public record CreateMenuItemRequest(string Name, string Category, long PriceCents);

public record ChangePriceRequest(long PriceCents);

public record SetAvailabilityRequest(bool IsAvailable);

public record MenuItemDto(Guid Id, string Name, string Category, long PriceCents, bool IsAvailable)
{
    public static MenuItemDto From(MenuItem item) =>
        new(item.Id, item.Name, item.Category, item.PriceCents, item.IsAvailable);
}
