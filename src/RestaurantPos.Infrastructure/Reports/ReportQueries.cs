using Dapper;
using Npgsql;
using RestaurantPos.Application.Reports;

namespace RestaurantPos.Infrastructure.Reports;

// Reports are hand-written SQL (Dapper) because they are read-only and
// aggregate-heavy: window functions and CTEs are clearer here than LINQ.
// Money is integer cents; discount and tax use the same half-up integer
// rounding as the C# Order class, so reports always match the receipts.
public class ReportQueries(string connectionString) : IReportQueries
{
    // Shared CTEs: one row per PAID order with its computed totals.
    // @offset shifts UTC timestamps to the restaurant's local time (minutes).
    private const string OrderTotalsCte = """
        WITH line_totals AS (
            SELECT order_id, SUM(unit_price_cents * quantity)::bigint AS subtotal
            FROM order_lines
            GROUP BY order_id
        ),
        priced AS (
            SELECT (o.paid_at AT TIME ZONE 'UTC') + make_interval(mins => @offset) AS paid_local,
                   lt.subtotal,
                   (lt.subtotal * o.discount_percent + 50) / 100 AS discount,
                   o.tax_rate_basis_points AS bp
            FROM orders o
            JOIN line_totals lt ON lt.order_id = o.id
            WHERE o.status = 'Paid'
        ),
        order_totals AS (
            SELECT paid_local, subtotal, discount,
                   ((subtotal - discount) * bp + 5000) / 10000 AS tax,
                   subtotal - discount + ((subtotal - discount) * bp + 5000) / 10000 AS total
            FROM priced
        )
        """;

    private const string DailySql = OrderTotalsCte + """
        ,
        daily AS (
            SELECT paid_local::date AS d,
                   COUNT(*)::int AS orders,
                   SUM(subtotal)::bigint AS gross,
                   SUM(discount)::bigint AS discounts,
                   SUM(tax)::bigint AS tax,
                   SUM(total)::bigint AS total
            FROM order_totals
            WHERE paid_local::date BETWEEN @fromDay::date AND @toDay::date
            GROUP BY paid_local::date
        )
        SELECT to_char(d, 'YYYY-MM-DD') AS "Day",
               orders AS "Orders",
               gross AS "GrossCents",
               discounts AS "DiscountCents",
               tax AS "TaxCents",
               total AS "TotalCents",
               ROUND(total::numeric / orders)::bigint AS "AvgOrderCents",
               SUM(total) OVER (ORDER BY d)::bigint AS "RunningTotalCents"
        FROM daily
        ORDER BY d
        """;

    private const string HourlySql = OrderTotalsCte + """
        ,
        hourly AS (
            SELECT EXTRACT(HOUR FROM paid_local)::int AS hr,
                   COUNT(*)::int AS orders,
                   SUM(total)::bigint AS total
            FROM order_totals
            WHERE paid_local::date BETWEEN @fromDay::date AND @toDay::date
            GROUP BY 1
        )
        SELECT hr AS "Hour",
               orders AS "Orders",
               total AS "TotalCents",
               ROUND(total * 100.0 / SUM(total) OVER (), 1) AS "SharePercent"
        FROM hourly
        ORDER BY hr
        """;

    // Item revenue here is line revenue (price x quantity) before order-level discount and tax.
    private const string TopItemsSql = """
        WITH item_sales AS (
            SELECT l.item_name,
                   SUM(l.quantity)::bigint AS qty,
                   SUM(l.unit_price_cents * l.quantity)::bigint AS revenue
            FROM order_lines l
            JOIN orders o ON o.id = l.order_id
            WHERE o.status = 'Paid'
              AND ((o.paid_at AT TIME ZONE 'UTC') + make_interval(mins => @offset))::date
                  BETWEEN @fromDay::date AND @toDay::date
            GROUP BY l.item_name
        )
        SELECT RANK() OVER (ORDER BY revenue DESC)::int AS "Rank",
               item_name AS "ItemName",
               qty AS "QuantitySold",
               revenue AS "RevenueCents",
               ROUND(revenue * 100.0 / SUM(revenue) OVER (), 1) AS "SharePercent"
        FROM item_sales
        ORDER BY "Rank", item_name
        LIMIT @limit
        """;

    public async Task<IReadOnlyList<DailySalesRow>> DailySalesAsync(
        DateOnly from, DateOnly to, int utcOffsetMinutes, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        var rows = await conn.QueryAsync<DailySalesRow>(new CommandDefinition(
            DailySql, Args(from, to, utcOffsetMinutes), cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<TopItemRow>> TopItemsAsync(
        DateOnly from, DateOnly to, int utcOffsetMinutes, int limit, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        var rows = await conn.QueryAsync<TopItemRow>(new CommandDefinition(
            TopItemsSql,
            new { fromDay = Day(from), toDay = Day(to), offset = utcOffsetMinutes, limit },
            cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<HourlySalesRow>> HourlySalesAsync(
        DateOnly from, DateOnly to, int utcOffsetMinutes, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        var rows = await conn.QueryAsync<HourlySalesRow>(new CommandDefinition(
            HourlySql, Args(from, to, utcOffsetMinutes), cancellationToken: ct));
        return rows.ToList();
    }

    private static string Day(DateOnly d) => d.ToString("yyyy-MM-dd");

    private static object Args(DateOnly from, DateOnly to, int offset) =>
        new { fromDay = Day(from), toDay = Day(to), offset };
}
