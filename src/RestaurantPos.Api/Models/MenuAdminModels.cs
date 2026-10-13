using System.ComponentModel.DataAnnotations;
using RestaurantPos.Application.Menu;

namespace RestaurantPos.Api.Models;

// Form for adding a menu item. Price is entered in normal units (12.50) and stored as cents.
public class CreateMenuItemForm
{
    [Required(ErrorMessage = "Name is required.")]
    [StringLength(120, ErrorMessage = "Name can be at most 120 characters.")]
    public string Name { get; set; } = "";

    [Required(ErrorMessage = "Category is required.")]
    [StringLength(60, ErrorMessage = "Category can be at most 60 characters.")]
    public string Category { get; set; } = "";

    [Required(ErrorMessage = "Price is required.")]
    [Range(typeof(decimal), "0.01", "100000", ErrorMessage = "Price must be between 0.01 and 100000.")]
    [Display(Name = "Price")]
    public decimal? Price { get; set; }

    public long PriceCents => (long)Math.Round((Price ?? 0) * 100, MidpointRounding.AwayFromZero);
}

public class MenuAdminViewModel
{
    public List<MenuItemDto> Items { get; init; } = [];
    public CreateMenuItemForm NewItem { get; init; } = new();
}
