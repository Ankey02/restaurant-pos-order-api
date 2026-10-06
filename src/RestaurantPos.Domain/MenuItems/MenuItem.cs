using RestaurantPos.Domain.Common;

namespace RestaurantPos.Domain.MenuItems;

public class MenuItem
{
    private MenuItem() { } // needed by EF Core

    public Guid Id { get; private set; }
    public string Name { get; private set; } = "";
    public string Category { get; private set; } = "";
    public long PriceCents { get; private set; }
    public bool IsAvailable { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public static MenuItem Create(string name, string category, long priceCents, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("Name is required.");
        if (string.IsNullOrWhiteSpace(category)) throw new DomainException("Category is required.");
        if (priceCents <= 0) throw new DomainException("Price must be greater than zero.");

        return new MenuItem
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Category = category.Trim(),
            PriceCents = priceCents,
            IsAvailable = true,
            CreatedAt = nowUtc
        };
    }

    public void ChangePrice(long priceCents)
    {
        if (priceCents <= 0) throw new DomainException("Price must be greater than zero.");
        PriceCents = priceCents;
    }

    public void SetAvailable(bool available) => IsAvailable = available;
}
