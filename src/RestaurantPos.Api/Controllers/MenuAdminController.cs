using Microsoft.AspNetCore.Mvc;
using RestaurantPos.Api.Models;
using RestaurantPos.Application.Common;
using RestaurantPos.Application.Menu;
using RestaurantPos.Domain.Common;

namespace RestaurantPos.Api.Controllers;

// ASP.NET MVC admin page for the menu (server-rendered Razor views).
// Adding an item is a classic form post with model validation and Post/Redirect/Get;
// price changes and availability toggles use jQuery AJAX calls to the REST API (/menu).
[Route("admin/menu")]
public class MenuAdminController(MenuCommands commands, MenuQueries queries) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct) =>
        View(new MenuAdminViewModel { Items = await queries.ListAsync(null, ct) });

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind(Prefix = "NewItem")] CreateMenuItemForm form, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            try
            {
                var item = await commands.CreateAsync(
                    new CreateMenuItemRequest(form.Name, form.Category, form.PriceCents), ct);
                TempData["Message"] = $"Added \"{item.Name}\".";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex) when (ex is ConflictException or DomainException)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
            }
        }

        // Show the same page again with the errors and what the user typed.
        return View(nameof(Index), new MenuAdminViewModel
        {
            Items = await queries.ListAsync(null, ct),
            NewItem = form
        });
    }
}
