using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace RestaurantPos.Tests;

[Collection("api")]
public class OrdersApiTests(PosApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    private static JsonElement Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage r)
    {
        var body = await r.Content.ReadAsStringAsync();
        Assert.True(r.IsSuccessStatusCode, $"{(int)r.StatusCode}: {body}");
        return Parse(body);
    }

    private async Task<Guid> CreateMenuItemAsync(string prefix, long priceCents)
    {
        var r = await _client.PostAsJsonAsync("/menu",
            new { name = $"{prefix}-{Guid.NewGuid():N}", category = "Test", priceCents });
        return (await ReadAsync(r)).GetProperty("id").GetGuid();
    }

    private async Task<Guid> OpenOrderAsync(int table)
    {
        var r = await _client.PostAsJsonAsync("/orders", new { tableNumber = table });
        return (await ReadAsync(r)).GetProperty("id").GetGuid();
    }

    private Task<HttpResponseMessage> AddLineAsync(Guid orderId, Guid menuItemId, int quantity)
        => _client.PostAsJsonAsync($"/orders/{orderId}/lines", new { menuItemId, quantity });

    [Fact]
    public async Task Full_flow_open_add_send_pay_returns_correct_totals_and_change()
    {
        var burger = await CreateMenuItemAsync("Burger", 1000);
        var fries = await CreateMenuItemAsync("Fries", 500);
        var orderId = await OpenOrderAsync(1);

        await ReadAsync(await AddLineAsync(orderId, burger, 2));
        var order = await ReadAsync(await AddLineAsync(orderId, fries, 1));

        // 2 x 1000 + 1 x 500 = 2500; 5% tax = 125; total 2625
        Assert.Equal(2500, order.GetProperty("subtotalCents").GetInt64());
        Assert.Equal(125, order.GetProperty("taxCents").GetInt64());
        Assert.Equal(2625, order.GetProperty("totalCents").GetInt64());

        var sent = await ReadAsync(await _client.PostAsync($"/orders/{orderId}/send", null));
        Assert.Equal("SentToKitchen", sent.GetProperty("status").GetString());

        var paid = await ReadAsync(await _client.PostAsJsonAsync(
            $"/orders/{orderId}/pay", new { method = "Cash", tenderedCents = 3000 }));
        Assert.Equal("Paid", paid.GetProperty("status").GetString());
        Assert.Equal(375, paid.GetProperty("changeCents").GetInt64());
    }

    [Fact]
    public async Task Second_active_order_for_the_same_table_returns_409()
    {
        await OpenOrderAsync(2);
        var second = await _client.PostAsJsonAsync("/orders", new { tableNumber = 2 });
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Table_can_be_used_again_after_the_order_is_paid()
    {
        var item = await CreateMenuItemAsync("Tea", 300);
        var orderId = await OpenOrderAsync(3);
        await ReadAsync(await AddLineAsync(orderId, item, 1));
        await ReadAsync(await _client.PostAsync($"/orders/{orderId}/send", null));
        await ReadAsync(await _client.PostAsJsonAsync(
            $"/orders/{orderId}/pay", new { method = "Cash", tenderedCents = 500 }));

        var again = await _client.PostAsJsonAsync("/orders", new { tableNumber = 3 });
        Assert.True(again.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Adding_a_line_after_sending_to_kitchen_returns_422()
    {
        var item = await CreateMenuItemAsync("Soup", 800);
        var orderId = await OpenOrderAsync(4);
        await ReadAsync(await AddLineAsync(orderId, item, 1));
        await ReadAsync(await _client.PostAsync($"/orders/{orderId}/send", null));

        var r = await AddLineAsync(orderId, item, 1);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, r.StatusCode);
    }

    [Fact]
    public async Task Paying_less_than_the_total_in_cash_returns_422()
    {
        var item = await CreateMenuItemAsync("Wrap", 1000);
        var orderId = await OpenOrderAsync(5);
        await ReadAsync(await AddLineAsync(orderId, item, 1)); // total 1050
        await ReadAsync(await _client.PostAsync($"/orders/{orderId}/send", null));

        var r = await _client.PostAsJsonAsync(
            $"/orders/{orderId}/pay", new { method = "Cash", tenderedCents = 1000 });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, r.StatusCode);
    }

    [Fact]
    public async Task Unknown_menu_item_returns_404()
    {
        var orderId = await OpenOrderAsync(6);
        var r = await AddLineAsync(orderId, Guid.NewGuid(), 1);
        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
    }

    [Fact]
    public async Task Unknown_order_returns_404()
    {
        var r = await _client.GetAsync($"/orders/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
    }

    [Fact]
    public async Task Duplicate_menu_item_name_returns_409()
    {
        var name = $"Unique-{Guid.NewGuid():N}";
        var first = await _client.PostAsJsonAsync("/menu", new { name, category = "Test", priceCents = 100 });
        Assert.True(first.IsSuccessStatusCode);
        var second = await _client.PostAsJsonAsync("/menu", new { name, category = "Test", priceCents = 200 });
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }
}
