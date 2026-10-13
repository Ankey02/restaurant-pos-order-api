using Microsoft.AspNetCore.Mvc;
using RestaurantPos.Application.Orders;

namespace RestaurantPos.Api.Controllers;

[ApiController]
[Route("orders")]
public class OrdersController(OrderCommands commands, OrderQueries queries, IOrderHistoryQueries history) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Open(OpenOrderRequest request, CancellationToken ct)
    {
        var order = await commands.OpenAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = order.Id }, order);
    }

    [HttpGet("{id:guid}")]
    public async Task<OrderDto> Get(Guid id, CancellationToken ct) =>
        await queries.GetAsync(id, ct);

    // Orders still being served (open or in the kitchen).
    [HttpGet]
    public async Task<List<OrderDto>> ListActive(CancellationToken ct) =>
        await queries.ListActiveAsync(ct);

    // Status changes recorded by the SQL Server audit trigger, oldest first.
    [HttpGet("{id:guid}/history")]
    public async Task<IReadOnlyList<OrderStatusChangeDto>> History(Guid id, CancellationToken ct) =>
        await history.GetAsync(id, ct);

    [HttpPost("{id:guid}/lines")]
    public async Task<OrderDto> AddLine(Guid id, AddLineRequest request, CancellationToken ct) =>
        await commands.AddLineAsync(id, request, ct);

    [HttpDelete("{id:guid}/lines/{menuItemId:guid}")]
    public async Task<OrderDto> RemoveLine(Guid id, Guid menuItemId, CancellationToken ct) =>
        await commands.RemoveLineAsync(id, menuItemId, ct);

    [HttpPut("{id:guid}/discount")]
    public async Task<OrderDto> ApplyDiscount(Guid id, DiscountRequest request, CancellationToken ct) =>
        await commands.ApplyDiscountAsync(id, request, ct);

    [HttpPost("{id:guid}/send")]
    public async Task<OrderDto> SendToKitchen(Guid id, CancellationToken ct) =>
        await commands.SendToKitchenAsync(id, ct);

    [HttpPost("{id:guid}/pay")]
    public async Task<OrderDto> Pay(Guid id, PayRequest request, CancellationToken ct) =>
        await commands.PayAsync(id, request, ct);

    [HttpPost("{id:guid}/cancel")]
    public async Task<OrderDto> Cancel(Guid id, CancelRequest request, CancellationToken ct) =>
        await commands.CancelAsync(id, request, ct);
}
