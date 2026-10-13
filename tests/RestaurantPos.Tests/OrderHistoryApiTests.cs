using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace RestaurantPos.Tests;

// The SQL Server trigger trg_orders_status_audit writes a row on every status change;
// GET /orders/{id}/history reads those rows back.
[Collection("api")]
public class OrderHistoryApiTests(PosApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage r)
    {
        var body = await r.Content.ReadAsStringAsync();
        Assert.True(r.IsSuccessStatusCode, $"{(int)r.StatusCode}: {body}");
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.Clone();
    }

    private async Task<Guid> OpenOrderAsync(int table) =>
        (await ReadAsync(await _client.PostAsJsonAsync("/orders", new { tableNumber = table })))
            .GetProperty("id").GetGuid();

    private static (string? From, string To) Step(JsonElement e) =>
        (e.GetProperty("fromStatus").ValueKind == JsonValueKind.Null ? null : e.GetProperty("fromStatus").GetString(),
         e.GetProperty("toStatus").GetString()!);

    [Fact]
    public async Task Trigger_records_every_status_change_in_order()
    {
        var menu = await ReadAsync(await _client.PostAsJsonAsync("/menu",
            new { name = $"Soup-{Guid.NewGuid():N}", category = "Test", priceCents = 400 }));
        var orderId = await OpenOrderAsync(40);

        var order = await ReadAsync(await _client.PostAsJsonAsync($"/orders/{orderId}/lines",
            new { menuItemId = menu.GetProperty("id").GetGuid(), quantity = 2 }));
        var total = order.GetProperty("totalCents").GetInt64();

        await ReadAsync(await _client.PostAsync($"/orders/{orderId}/send", null));
        await ReadAsync(await _client.PostAsJsonAsync($"/orders/{orderId}/pay",
            new { method = "Card", tenderedCents = total }));

        var history = (await ReadAsync(await _client.GetAsync($"/orders/{orderId}/history")))
            .EnumerateArray().Select(Step).ToList();

        // Adding a line does not change the status, so it is not logged.
        Assert.Equal(
            new (string?, string)[] { (null, "Open"), ("Open", "SentToKitchen"), ("SentToKitchen", "Paid") },
            history);
    }

    [Fact]
    public async Task Cancelling_an_order_is_recorded()
    {
        var orderId = await OpenOrderAsync(41);
        await ReadAsync(await _client.PostAsJsonAsync($"/orders/{orderId}/cancel", new { reason = "Customer left" }));

        var history = (await ReadAsync(await _client.GetAsync($"/orders/{orderId}/history")))
            .EnumerateArray().ToList();

        Assert.Equal(2, history.Count);
        Assert.Equal(("Open", "Cancelled"), Step(history[1]));
        Assert.EndsWith("Z", history[1].GetProperty("changedAt").GetString()); // returned as UTC
    }

    [Fact]
    public async Task History_of_an_unknown_order_returns_404()
    {
        var r = await _client.GetAsync($"/orders/{Guid.NewGuid()}/history");
        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
    }
}
