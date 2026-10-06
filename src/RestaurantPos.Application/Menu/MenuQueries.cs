using Microsoft.EntityFrameworkCore;
using RestaurantPos.Application.Common;

namespace RestaurantPos.Application.Menu;

public class MenuQueries(IAppDbContext db)
{
    public async Task<List<MenuItemDto>> ListAsync(string? category, CancellationToken ct)
    {
        var query = db.MenuItems.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(m => m.Category == category.Trim());

        return await query
            .OrderBy(m => m.Category).ThenBy(m => m.Name)
            .Select(m => new MenuItemDto(m.Id, m.Name, m.Category, m.PriceCents, m.IsAvailable))
            .ToListAsync(ct);
    }

    public async Task<MenuItemDto> GetAsync(Guid id, CancellationToken ct) =>
        await db.MenuItems.AsNoTracking()
            .Where(m => m.Id == id)
            .Select(m => new MenuItemDto(m.Id, m.Name, m.Category, m.PriceCents, m.IsAvailable))
            .FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException("Menu item not found.");
}
