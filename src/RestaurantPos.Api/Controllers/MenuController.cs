using Microsoft.AspNetCore.Mvc;
using RestaurantPos.Application.Menu;

namespace RestaurantPos.Api.Controllers;

[ApiController]
[Route("menu")]
public class MenuController(MenuCommands commands, MenuQueries queries) : ControllerBase
{
    [HttpGet]
    public async Task<List<MenuItemDto>> List(string? category, CancellationToken ct) =>
        await queries.ListAsync(category, ct);

    [HttpGet("{id:guid}")]
    public async Task<MenuItemDto> Get(Guid id, CancellationToken ct) =>
        await queries.GetAsync(id, ct);

    [HttpPost]
    public async Task<IActionResult> Create(CreateMenuItemRequest request, CancellationToken ct)
    {
        var item = await commands.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = item.Id }, item);
    }

    [HttpPut("{id:guid}/price")]
    public async Task<MenuItemDto> ChangePrice(Guid id, ChangePriceRequest request, CancellationToken ct) =>
        await commands.ChangePriceAsync(id, request, ct);

    [HttpPut("{id:guid}/availability")]
    public async Task<MenuItemDto> SetAvailability(Guid id, SetAvailabilityRequest request, CancellationToken ct) =>
        await commands.SetAvailabilityAsync(id, request, ct);
}
