using Microsoft.EntityFrameworkCore;
using RestaurantPos.Application.Common;
using RestaurantPos.Domain.MenuItems;

namespace RestaurantPos.Application.Menu;

public class MenuCommands(IAppDbContext db, TimeProvider clock)
{
    public async Task<MenuItemDto> CreateAsync(CreateMenuItemRequest request, CancellationToken ct)
    {
        var item = MenuItem.Create(request.Name, request.Category, request.PriceCents, clock.GetUtcNow().UtcDateTime);

        if (await db.MenuItems.AnyAsync(m => m.Name == item.Name, ct))
            throw new ConflictException("A menu item with this name already exists.");

        db.MenuItems.Add(item);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Two people added the same name at the same moment: the database's unique index caught it.
            throw new ConflictException("A menu item with this name already exists.");
        }

        return MenuItemDto.From(item);
    }

    public async Task<MenuItemDto> ChangePriceAsync(Guid id, ChangePriceRequest request, CancellationToken ct)
    {
        var item = await FindAsync(id, ct);
        item.ChangePrice(request.PriceCents);
        await db.SaveChangesAsync(ct);
        return MenuItemDto.From(item);
    }

    public async Task<MenuItemDto> SetAvailabilityAsync(Guid id, SetAvailabilityRequest request, CancellationToken ct)
    {
        var item = await FindAsync(id, ct);
        item.SetAvailable(request.IsAvailable);
        await db.SaveChangesAsync(ct);
        return MenuItemDto.From(item);
    }

    private async Task<MenuItem> FindAsync(Guid id, CancellationToken ct) =>
        await db.MenuItems.FirstOrDefaultAsync(m => m.Id == id, ct)
        ?? throw new NotFoundException("Menu item not found.");
}
