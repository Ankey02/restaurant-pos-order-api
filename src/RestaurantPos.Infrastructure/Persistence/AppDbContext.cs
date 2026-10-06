using Microsoft.EntityFrameworkCore;
using RestaurantPos.Application.Common;
using RestaurantPos.Domain.MenuItems;
using RestaurantPos.Domain.Orders;

namespace RestaurantPos.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<MenuItem> MenuItems => Set<MenuItem>();
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
}
