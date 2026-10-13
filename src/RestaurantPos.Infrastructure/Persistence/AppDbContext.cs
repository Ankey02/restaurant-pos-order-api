using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
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

    // All times are stored in UTC. SQL Server's datetime2 does not remember that,
    // so values are marked as UTC again when they are read back (JSON keeps the "Z").
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Covers DateTime and DateTime? (EF never passes null to a converter).
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
    }
}

public class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    v => v,
    v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

