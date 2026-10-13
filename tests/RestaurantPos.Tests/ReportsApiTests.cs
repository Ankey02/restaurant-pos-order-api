using System.Net;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Xunit;

namespace RestaurantPos.Tests;

// Four paid orders are inserted on fixed dates in 2020, so no other test can
// affect them. Expected numbers were worked out by hand with the same rules
// as the app: discount = (sub*pct+50)/100, tax = ((sub-discount)*bp+5000)/10000.
//
//  A 2020-01-15 12:10Z  2 Burger@1000 + 1 Fries@500      sub 2500 disc   0 tax 125 total 2625
//  B 2020-01-15 12:50Z  1 Burger@1000, 10% discount      sub 1000 disc 100 tax  45 total  945
//  C 2020-01-15 19:30Z  1 Pizza@1200 + 1 Fries@500       sub 1700 disc   0 tax  85 total 1785
//  D 2020-01-16 12:05Z  1 Burger@1000                    sub 1000 disc   0 tax  50 total 1050
[Collection("api")]
public class ReportsApiTests(PosApiFactory factory)
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static bool _seeded;

    private async Task<HttpClient> ClientAsync()
    {
        var client = factory.CreateClient(); // starts the app, which applies migrations
        await Gate.WaitAsync();
        try
        {
            if (!_seeded)
            {
                await InsertPaidOrderAsync("2020-01-15T12:10:00Z", 0, ("Burger", 1000, 2), ("Fries", 500, 1));
                await InsertPaidOrderAsync("2020-01-15T12:50:00Z", 10, ("Burger", 1000, 1));
                await InsertPaidOrderAsync("2020-01-15T19:30:00Z", 0, ("Pizza", 1200, 1), ("Fries", 500, 1));
                await InsertPaidOrderAsync("2020-01-16T12:05:00Z", 0, ("Burger", 1000, 1));
                _seeded = true;
            }
        }
        finally { Gate.Release(); }
        return client;
    }

    private async Task InsertPaidOrderAsync(string paidAtUtc, int discountPercent,
        params (string Name, long Price, int Qty)[] lines)
    {
        await using var conn = new SqlConnection(factory.ConnectionString);
        await conn.OpenAsync();
        var orderId = Guid.NewGuid();
        var paidAt = DateTimeOffset.Parse(paidAtUtc).UtcDateTime;

        await using (var cmd = new SqlCommand("""
            INSERT INTO orders (id, table_number, status, tax_rate_basis_points, discount_percent,
                                created_at, paid_at, paid_with, tendered_cents, change_cents)
            VALUES (@id, 1, N'Paid', 500, @disc, @paid, @paid, N'Card', 0, 0)
            """, conn))
        {
            cmd.Parameters.AddWithValue("@id", orderId);
            cmd.Parameters.AddWithValue("@disc", discountPercent);
            cmd.Parameters.AddWithValue("@paid", paidAt);
            await cmd.ExecuteNonQueryAsync();
        }

        foreach (var (name, price, qty) in lines)
        {
            await using var cmd = new SqlCommand("""
                INSERT INTO order_lines (id, order_id, menu_item_id, item_name, unit_price_cents, quantity)
                VALUES (@id, @order, @item, @name, @price, @qty)
                """, conn);
            cmd.Parameters.AddWithValue("@id", Guid.NewGuid());
            cmd.Parameters.AddWithValue("@order", orderId);
            cmd.Parameters.AddWithValue("@item", Guid.NewGuid());
            cmd.Parameters.AddWithValue("@name", name);
            cmd.Parameters.AddWithValue("@price", price);
            cmd.Parameters.AddWithValue("@qty", qty);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    private static async Task<List<JsonElement>> GetRowsAsync(HttpClient client, string url)
    {
        var r = await client.GetAsync(url);
        var body = await r.Content.ReadAsStringAsync();
        Assert.True(r.IsSuccessStatusCode, $"{(int)r.StatusCode}: {body}");
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.EnumerateArray().Select(e => e.Clone()).ToList();
    }

    [Fact]
    public async Task Daily_sales_totals_match_hand_calculation_with_running_total()
    {
        var client = await ClientAsync();
        var rows = await GetRowsAsync(client,
            "/reports/daily-sales?fromDate=2020-01-15&toDate=2020-01-16&utcOffsetMinutes=0");

        Assert.Equal(2, rows.Count);

        var d1 = rows[0];
        Assert.Equal("2020-01-15", d1.GetProperty("day").GetString());
        Assert.Equal(3, d1.GetProperty("orders").GetInt32());
        Assert.Equal(5200, d1.GetProperty("grossCents").GetInt64());
        Assert.Equal(100, d1.GetProperty("discountCents").GetInt64());
        Assert.Equal(255, d1.GetProperty("taxCents").GetInt64());
        Assert.Equal(5355, d1.GetProperty("totalCents").GetInt64());
        Assert.Equal(1785, d1.GetProperty("avgOrderCents").GetInt64());
        Assert.Equal(5355, d1.GetProperty("runningTotalCents").GetInt64());

        var d2 = rows[1];
        Assert.Equal("2020-01-16", d2.GetProperty("day").GetString());
        Assert.Equal(1050, d2.GetProperty("totalCents").GetInt64());
        Assert.Equal(6405, d2.GetProperty("runningTotalCents").GetInt64()); // 5355 + 1050
    }

    [Fact]
    public async Task Top_items_are_ranked_by_revenue_with_percentage_share()
    {
        var client = await ClientAsync();
        var rows = await GetRowsAsync(client,
            "/reports/top-items?fromDate=2020-01-15&toDate=2020-01-16&utcOffsetMinutes=0");

        Assert.Equal(3, rows.Count);

        Assert.Equal(1, rows[0].GetProperty("rank").GetInt32());
        Assert.Equal("Burger", rows[0].GetProperty("itemName").GetString());
        Assert.Equal(4, rows[0].GetProperty("quantitySold").GetInt64());
        Assert.Equal(4000, rows[0].GetProperty("revenueCents").GetInt64());
        Assert.Equal(64.5m, rows[0].GetProperty("sharePercent").GetDecimal());

        Assert.Equal(2, rows[1].GetProperty("rank").GetInt32());
        Assert.Equal("Pizza", rows[1].GetProperty("itemName").GetString());
        Assert.Equal(1200, rows[1].GetProperty("revenueCents").GetInt64());

        Assert.Equal(3, rows[2].GetProperty("rank").GetInt32());
        Assert.Equal("Fries", rows[2].GetProperty("itemName").GetString());
        Assert.Equal(1000, rows[2].GetProperty("revenueCents").GetInt64());
        Assert.Equal(16.1m, rows[2].GetProperty("sharePercent").GetDecimal());
    }

    [Fact]
    public async Task Sales_by_hour_groups_orders_and_shows_share_of_the_day()
    {
        var client = await ClientAsync();
        var rows = await GetRowsAsync(client,
            "/reports/sales-by-hour?fromDate=2020-01-15&toDate=2020-01-15&utcOffsetMinutes=0");

        Assert.Equal(2, rows.Count);

        Assert.Equal(12, rows[0].GetProperty("hour").GetInt32());
        Assert.Equal(2, rows[0].GetProperty("orders").GetInt32());
        Assert.Equal(3570, rows[0].GetProperty("totalCents").GetInt64()); // 2625 + 945
        Assert.Equal(66.7m, rows[0].GetProperty("sharePercent").GetDecimal());

        Assert.Equal(19, rows[1].GetProperty("hour").GetInt32());
        Assert.Equal(1785, rows[1].GetProperty("totalCents").GetInt64());
        Assert.Equal(33.3m, rows[1].GetProperty("sharePercent").GetDecimal());
    }

    [Fact]
    public async Task Utc_offset_moves_sales_into_the_local_day_and_hour()
    {
        // India is UTC+05:30. Order A (12:10Z) is 17:40 local, B (12:50Z) is 18:20 local,
        // but C (19:30Z) is 01:00 on 2020-01-16 local, so it must NOT be in 2020-01-15.
        var client = await ClientAsync();
        var rows = await GetRowsAsync(client,
            "/reports/sales-by-hour?fromDate=2020-01-15&toDate=2020-01-15&utcOffsetMinutes=330");

        Assert.Equal(2, rows.Count);
        Assert.Equal(17, rows[0].GetProperty("hour").GetInt32());
        Assert.Equal(2625, rows[0].GetProperty("totalCents").GetInt64());
        Assert.Equal(18, rows[1].GetProperty("hour").GetInt32());
        Assert.Equal(945, rows[1].GetProperty("totalCents").GetInt64());
    }

    [Fact]
    public async Task Invalid_report_inputs_return_400()
    {
        var client = await ClientAsync();

        var reversed = await client.GetAsync("/reports/daily-sales?fromDate=2020-02-01&toDate=2020-01-01");
        Assert.Equal(HttpStatusCode.BadRequest, reversed.StatusCode);

        var badLimit = await client.GetAsync("/reports/top-items?limit=0");
        Assert.Equal(HttpStatusCode.BadRequest, badLimit.StatusCode);

        var badOffset = await client.GetAsync("/reports/daily-sales?utcOffsetMinutes=5000");
        Assert.Equal(HttpStatusCode.BadRequest, badOffset.StatusCode);
    }
}
