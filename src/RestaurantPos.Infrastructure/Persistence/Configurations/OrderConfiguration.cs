using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantPos.Domain.Orders;

namespace RestaurantPos.Infrastructure.Persistence.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> b)
    {
        b.ToTable("orders");
        b.HasKey(x => x.Id);

        // The Id is created in code (Guid.NewGuid), so EF must not try to generate it.
        // Without this, a new line added to an existing order would be treated as an UPDATE.
        b.Property(x => x.Id).ValueGeneratedNever();

        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.PaidWith).HasConversion<string>().HasMaxLength(10);
        b.Property(x => x.CancelReason).HasMaxLength(200);

        // SQL Server rowversion column: the database changes it on every update.
        // Two people changing one order at once: the second one is rejected (409).
        b.Property<byte[]>("RowVersion").IsRowVersion().IsRequired();

        b.HasMany(x => x.Lines)
            .WithOne()
            .HasForeignKey(l => l.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Navigation(x => x.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);

        // A table can have only ONE active order. The database enforces this, not just the code
        // (a SQL Server filtered unique index).
        b.HasIndex(x => x.TableNumber)
            .IsUnique()
            .HasFilter("[status] IN (N'Open', N'SentToKitchen')");

        b.HasIndex(x => x.CreatedAt);
        b.HasIndex(x => x.PaidAt);
    }
}
