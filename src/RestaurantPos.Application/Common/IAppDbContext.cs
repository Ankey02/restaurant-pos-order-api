using Microsoft.EntityFrameworkCore;
using RestaurantPos.Domain.MenuItems;
using RestaurantPos.Domain.Orders;

namespace RestaurantPos.Application.Common;

// The Application layer only knows this interface, not PostgreSQL.
public interface IAppDbContext
{
    DbSet<MenuItem> MenuItems { get; }
    DbSet<Order> Orders { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
